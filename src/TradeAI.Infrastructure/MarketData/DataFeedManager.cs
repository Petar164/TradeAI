using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradeAI.Core.Events;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Core.Models;
using TradeAI.Infrastructure.Settings;

namespace TradeAI.Infrastructure.MarketData;

/// <summary>
/// Background service that drives all market data polling.
///
/// Two loops:
///   1. Active-symbol loop  — polls every <see cref="AppSettings.IntraCandlePollMs"/> ms.
///      Detects intra-candle updates AND candle closes by comparing OpenTime.
///   2. Watchlist loop      — refreshes every 60 s, one symbol at a time with a
///      <see cref="AppSettings.WatchlistBatchDelayMs"/> ms gap between symbols.
///
/// Events are plain C# EventHandler until Sprint 5 replaces them with SignalBus.
/// </summary>
public sealed class DataFeedManager : IHostedService, ILiveCandleFeed, IDisposable
{
    // ── Events (replaced by SignalBus in Sprint 5) ────────────────────────────
    public event EventHandler<CandleEventArgs>? IntraCandleUpdated;
    public event EventHandler<CandleEventArgs>? CandleClosed;

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IMarketDataProvider      _provider;
    private readonly CandleCache              _cache;
    private readonly RateLimitScheduler       _scheduler;
    private readonly ICandleWriter            _candleWriter;
    private readonly IWatchlistReader         _watchlistReader;
    private readonly AppSettings              _settings;
    private readonly SignalBus                _bus;
    private readonly ILogger<DataFeedManager> _logger;

    // ── State ─────────────────────────────────────────────────────────────────
    private CancellationTokenSource? _cts;
    private Task? _activeTask;
    private Task? _watchlistTask;

    private volatile string _activeSymbol;
    private volatile string _activeTimeframe;
    private DateTimeOffset? _lastActiveOpenTime;
    private volatile bool   _reloadRequested;
    private CancellationTokenSource? _pollDelayCts;  // cancelled to skip the 5 s wait

    // ── Constructor ───────────────────────────────────────────────────────────
    public DataFeedManager(
        IMarketDataProvider      provider,
        CandleCache              cache,
        RateLimitScheduler       scheduler,
        ICandleWriter            candleWriter,
        IWatchlistReader         watchlistReader,
        AppSettings              settings,
        SignalBus                bus,
        ILogger<DataFeedManager> logger)
    {
        _provider        = provider;
        _cache           = cache;
        _scheduler       = scheduler;
        _candleWriter    = candleWriter;
        _watchlistReader = watchlistReader;
        _settings        = settings;
        _bus             = bus;
        _logger          = logger;
        _activeSymbol    = settings.ActiveSymbol;
        _activeTimeframe = settings.ActiveTimeframe;
    }

    // ── IHostedService ────────────────────────────────────────────────────────

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Load the initial candles synchronously before returning so the chart
        // has data when the UI first renders.
        await LoadInitialCandlesAsync(_activeSymbol, _activeTimeframe, _cts.Token);

        _activeTask   = RunActiveSymbolLoopAsync(_cts.Token);
        _watchlistTask = RunWatchlistLoopAsync(_cts.Token);

        _logger.LogInformation("DataFeedManager started — {Symbol} {Timeframe}",
            _activeSymbol, _activeTimeframe);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        try
        {
            var tasks = new[] { _activeTask, _watchlistTask }
                .Where(t => t != null).Cast<Task>().ToArray();
            if (tasks.Length > 0) await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }

        _logger.LogInformation("DataFeedManager stopped.");
    }

    // ── Public control ────────────────────────────────────────────────────────

    /// <summary>Switch the active chart symbol. Triggers an immediate candle reload.</summary>
    public void SetActiveSymbol(string symbol, string timeframe)
    {
        _activeSymbol    = symbol;
        _activeTimeframe = timeframe;
        _lastActiveOpenTime = null;
        _settings.ActiveSymbol    = symbol;
        _settings.ActiveTimeframe = timeframe;
        _reloadRequested = true;

        // Cancel any running poll delay so the reload happens immediately
        _pollDelayCts?.Cancel();

        _bus.Publish(new ActiveSymbolChangedEvent(symbol, timeframe));
        _logger.LogInformation("Active symbol changed → {Symbol} {Timeframe}", symbol, timeframe);
    }

    // ── Active-symbol polling loop ────────────────────────────────────────────

    private async Task RunActiveSymbolLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Use a short-lived CTS so SetActiveSymbol can cancel the delay early
                _pollDelayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                try   { await Task.Delay(_settings.IntraCandlePollMs, _pollDelayCts.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* reload early */ }
                finally { _pollDelayCts.Dispose(); _pollDelayCts = null; }

                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    await LoadInitialCandlesAsync(_activeSymbol, _activeTimeframe, ct);
                    continue;
                }

                if (_scheduler.IsOpen) continue;

                await _scheduler.AcquireAsync(ct);
                var partial = await _provider.GetLatestPartialAsync(
                    _activeSymbol, _activeTimeframe, ct);
                _scheduler.RecordSuccess();

                if (partial == null) continue;

                // ── Candle-close detection ────────────────────────────────────
                if (_lastActiveOpenTime.HasValue &&
                    partial.OpenTime != _lastActiveOpenTime.Value)
                {
                    // The previous candle just closed — persist + notify
                    var completed = _cache.GetLast(_activeSymbol, _activeTimeframe);
                    if (completed != null)
                    {
                        await _candleWriter.UpsertAsync(completed);
                        var closedArgs = new CandleEventArgs
                        {
                            Symbol    = _activeSymbol,
                            Timeframe = _activeTimeframe,
                            Candle    = completed,
                        };
                        CandleClosed?.Invoke(this, closedArgs);
                        _bus.Publish(new CandleClosedEvent(_activeSymbol, _activeTimeframe, completed));
                        _logger.LogDebug("Candle closed {Symbol} {Timeframe} @ {Time}",
                            _activeSymbol, _activeTimeframe, completed.OpenTime);
                    }
                }

                _lastActiveOpenTime = partial.OpenTime;
                _cache.UpdateLastOrAppend(_activeSymbol, _activeTimeframe, partial);

                var intraCandleArgs = new CandleEventArgs
                {
                    Symbol    = _activeSymbol,
                    Timeframe = _activeTimeframe,
                    Candle    = partial,
                };
                IntraCandleUpdated?.Invoke(this, intraCandleArgs);
                _bus.Publish(new IntraCandleUpdateEvent(_activeSymbol, _activeTimeframe, partial));
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _scheduler.RecordFailure();
                _logger.LogWarning(ex, "Active symbol poll error for {Symbol}", _activeSymbol);
            }
        }
    }

    // ── Watchlist background loop ─────────────────────────────────────────────

    private async Task RunWatchlistLoopAsync(CancellationToken ct)
    {
        // Give the active loop a head-start before hammering the watchlist
        await Task.Delay(15_000, ct).ContinueWith(_ => { }, CancellationToken.None);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var items = await _watchlistReader.GetAllAsync();

                foreach (var item in items)
                {
                    if (ct.IsCancellationRequested) break;
                    if (item.Symbol == _activeSymbol) continue;   // active loop handles this
                    if (_scheduler.IsOpen) break;

                    await _scheduler.AcquireAsync(ct);
                    var partial = await _provider.GetLatestPartialAsync(
                        item.Symbol, _activeTimeframe, ct);
                    _scheduler.RecordSuccess();

                    if (partial == null) continue;

                    var lastCached = _cache.GetLast(item.Symbol, _activeTimeframe);
                    if (lastCached == null || partial.OpenTime > lastCached.OpenTime)
                    {
                        _cache.UpdateLastOrAppend(item.Symbol, _activeTimeframe, partial);
                        await _candleWriter.UpsertAsync(partial);
                        CandleClosed?.Invoke(this, new CandleEventArgs
                        {
                            Symbol    = item.Symbol,
                            Timeframe = _activeTimeframe,
                            Candle    = partial,
                        });
                        _bus.Publish(new CandleClosedEvent(item.Symbol, _activeTimeframe, partial));
                    }

                    await Task.Delay(_settings.WatchlistBatchDelayMs, ct);
                }

                await Task.Delay(60_000, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Watchlist poll error");
                await Task.Delay(5_000, ct).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }

    // ── Initial load ──────────────────────────────────────────────────────────

    private async Task LoadInitialCandlesAsync(string symbol, string timeframe, CancellationToken ct)
    {
        try
        {
            await _scheduler.AcquireAsync(ct);
            var candles = await _provider.GetHistoricalAsync(symbol, timeframe, 200, ct);
            _scheduler.RecordSuccess();

            if (candles.Count == 0)
            {
                _logger.LogWarning("No historical candles returned for {Symbol}/{Timeframe}",
                    symbol, timeframe);
                return;
            }

            _cache.Set(symbol, timeframe, candles);
            await _candleWriter.UpsertBatchAsync(candles);
            _lastActiveOpenTime = candles[^1].OpenTime;

            _logger.LogInformation("Loaded {Count} candles for {Symbol}/{Timeframe}",
                candles.Count, symbol, timeframe);

            _bus.Publish(new HistoricalDataReadyEvent(symbol, timeframe));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _scheduler.RecordFailure();
            _logger.LogWarning(ex, "Initial candle load failed for {Symbol}/{Timeframe}",
                symbol, timeframe);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
