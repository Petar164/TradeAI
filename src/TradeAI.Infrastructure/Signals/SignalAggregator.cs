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
/// (first match wins), checks for duplicate active signals (same symbol+direction),
/// persists to DB, then publishes a <see cref="SignalDetectedEvent"/> so the UI draws the overlay.
///
/// Sprint 7: duplicate prevention — if an active signal already exists for the same
/// symbol + direction, the new signal is discarded.  The active-signal map is updated
/// when <see cref="OverlayStateChangedEvent"/> fires with a terminal state.
/// </summary>
public sealed class SignalAggregator
{
    private readonly IReadOnlyList<ISignalDetector> _detectors;
    private readonly CandleCache                    _cache;
    private readonly ISignalStore                   _signalStore;
    private readonly IActiveSymbolProvider          _symbolProvider;
    private readonly SignalBus                      _bus;
    private readonly ILogger<SignalAggregator>      _logger;

    // Duplicate-signal guard: id → (symbol, direction)
    private readonly Dictionary<int, (string Symbol, TradeDirection Direction)> _activeSignals = new();
    private readonly object _activeLock = new();

    // Strong references so SignalBus WeakReferences stay alive
    private readonly Action<CandleClosedEvent>        _onCandleClosed;
    private readonly Action<OverlayStateChangedEvent> _onOverlayStateChanged;

#pragma warning disable IDE0052
    private readonly IDisposable _subClosed;
    private readonly IDisposable _subOverlay;
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

        _onCandleClosed        = OnCandleClosed;
        _onOverlayStateChanged = OnOverlayStateChanged;

        _subClosed  = _bus.Subscribe<CandleClosedEvent>(_onCandleClosed);
        _subOverlay = _bus.Subscribe<OverlayStateChangedEvent>(_onOverlayStateChanged);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnCandleClosed(CandleClosedEvent e)
    {
        if (e.Symbol    != _symbolProvider.ActiveSymbol    ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        _ = RunAsync(e.Symbol, e.Timeframe);
    }

    private void OnOverlayStateChanged(OverlayStateChangedEvent e)
    {
        if (e.NewState is OverlayState.TargetHit or OverlayState.StopHit or OverlayState.Expired)
        {
            lock (_activeLock) { _activeSignals.Remove(e.SignalId); }
        }
    }

    // ── Detection loop ────────────────────────────────────────────────────────

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

            // ── Duplicate guard ───────────────────────────────────────────────
            lock (_activeLock)
            {
                bool duplicate = _activeSignals.Values.Any(
                    v => v.Symbol == symbol && v.Direction == signal.Direction);
                if (duplicate)
                {
                    _logger.LogDebug("Duplicate {Type} {Direction} signal for {Symbol} — skipped",
                        signal.SignalType, signal.Direction, symbol);
                    return;
                }
            }

            // Persist and get DB-assigned id
            var id = await _signalStore.InsertAsync(signal);
            signal = signal with { Id = id };

            lock (_activeLock) { _activeSignals[id] = (symbol, signal.Direction); }

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
