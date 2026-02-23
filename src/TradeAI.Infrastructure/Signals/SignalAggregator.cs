using Microsoft.Extensions.Logging;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Core.Models;
using TradeAI.Infrastructure.MarketData;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Subscribes to <see cref="CandleClosedEvent"/> via the SignalBus.
/// On each close it runs all registered <see cref="ISignalDetector"/> implementations
/// (first match wins), persists the signal to the DB, then publishes a
/// <see cref="SignalDetectedEvent"/> so the UI can draw the overlay.
///
/// Only fires for the active symbol/timeframe to keep CPU usage low in Sprint 6.
/// Sprint 7 will extend this to run on all watchlist symbols.
/// </summary>
public sealed class SignalAggregator
{
    private readonly IReadOnlyList<ISignalDetector> _detectors;
    private readonly CandleCache                    _cache;
    private readonly ISignalStore                   _signalStore;
    private readonly IActiveSymbolProvider          _symbolProvider;
    private readonly SignalBus                      _bus;
    private readonly ILogger<SignalAggregator>      _logger;

    // Strong reference so SignalBus WeakReference stays alive
    private readonly Action<CandleClosedEvent> _onCandleClosed;
#pragma warning disable IDE0052
    private readonly IDisposable _sub;
#pragma warning restore IDE0052

    public SignalAggregator(
        IEnumerable<ISignalDetector>    detectors,
        CandleCache                     cache,
        ISignalStore                    signalStore,
        IActiveSymbolProvider           symbolProvider,
        SignalBus                       bus,
        ILogger<SignalAggregator>       logger)
    {
        _detectors      = detectors.ToList();
        _cache          = cache;
        _signalStore    = signalStore;
        _symbolProvider = symbolProvider;
        _bus            = bus;
        _logger         = logger;

        _onCandleClosed = OnCandleClosed;
        _sub            = _bus.Subscribe<CandleClosedEvent>(_onCandleClosed);
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnCandleClosed(CandleClosedEvent e)
    {
        // Sprint 6: only scan for the active symbol/timeframe
        if (e.Symbol    != _symbolProvider.ActiveSymbol    ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        // Fire-and-forget — detection is fast but involves async DB insert
        _ = RunAsync(e.Symbol, e.Timeframe);
    }

    private async Task RunAsync(string symbol, string timeframe)
    {
        try
        {
            var candles = _cache.TryGet(symbol, timeframe);
            if (candles == null || candles.Count < 60) return;

            Signal? signal = null;
            foreach (var detector in _detectors)
            {
                signal = await detector.DetectAsync(candles, symbol, timeframe);
                if (signal != null) break;
            }

            if (signal == null) return;

            // Persist and get DB-assigned id
            var id = await _signalStore.InsertAsync(signal);
            signal = signal with { Id = id };

            _logger.LogInformation("Signal detected: {Type} {Symbol} {Timeframe} id={Id}",
                signal.SignalType, symbol, timeframe, id);

            _bus.Publish(new SignalDetectedEvent(signal));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalAggregator error for {Symbol}/{Timeframe}", symbol, timeframe);
        }
    }
}
