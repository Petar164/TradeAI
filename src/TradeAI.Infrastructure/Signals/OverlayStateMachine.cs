using Microsoft.Extensions.Logging;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Tracks all live signal overlays in memory and drives their state transitions.
///
/// State machine:
///   Pending  → Active      : intra-candle price enters the entry zone
///   Active   → TargetHit   : intra-candle High reaches TargetLow
///   Active   → StopHit     : intra-candle Low reaches StopPrice
///   Any      → Expired     : TtlCandles candle-closes have elapsed for that symbol/timeframe
///
/// On every transition:
///   • DB state updated via ISignalStore (fire-and-forget)
///   • OverlayStateChangedEvent published on the SignalBus
///
/// Terminal states (TargetHit, StopHit, Expired) remove the overlay from tracking.
///
/// Sprint 8: on TargetHit / StopHit the binary outcome is recorded via
///   <see cref="ISimilarityEngine"/> so future kNN lookups have labelled data.
/// </summary>
public sealed class OverlayStateMachine
{
    private sealed record Entry(Signal Signal, int CandlesElapsed);

    private readonly Dictionary<int, Entry>        _overlays = new();
    private readonly object                        _lock     = new();
    private readonly ISignalStore                  _store;
    private readonly ISimilarityEngine             _similarity;
    private readonly SignalBus                     _bus;
    private readonly ILogger<OverlayStateMachine>  _logger;

    // Strong references for SignalBus WeakReferences
    private readonly Action<SignalDetectedEvent>     _onSignalDetected;
    private readonly Action<CandleClosedEvent>       _onCandleClosed;
    private readonly Action<IntraCandleUpdateEvent>  _onIntraCandle;

#pragma warning disable IDE0052
    private readonly IDisposable _subSignal;
    private readonly IDisposable _subClosed;
    private readonly IDisposable _subIntra;
#pragma warning restore IDE0052

    public OverlayStateMachine(
        ISignalStore                   store,
        ISimilarityEngine              similarity,
        SignalBus                      bus,
        ILogger<OverlayStateMachine>   logger)
    {
        _store      = store;
        _similarity = similarity;
        _bus        = bus;
        _logger     = logger;

        _onSignalDetected = OnSignalDetected;
        _onCandleClosed   = OnCandleClosed;
        _onIntraCandle    = OnIntraCandleUpdated;

        _subSignal = _bus.Subscribe<SignalDetectedEvent>(_onSignalDetected);
        _subClosed = _bus.Subscribe<CandleClosedEvent>(_onCandleClosed);
        _subIntra  = _bus.Subscribe<IntraCandleUpdateEvent>(_onIntraCandle);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnSignalDetected(SignalDetectedEvent e)
    {
        lock (_lock)
        {
            _overlays[e.Signal.Id] = new Entry(e.Signal, 0);
        }
        _logger.LogDebug("StateMachine tracking signal id={Id}", e.Signal.Id);
    }

    private void OnCandleClosed(CandleClosedEvent e)
    {
        List<int> toExpire = [];

        lock (_lock)
        {
            foreach (var (id, entry) in _overlays)
            {
                if (entry.Signal.Symbol    != e.Symbol    ||
                    entry.Signal.Timeframe != e.Timeframe) continue;

                int next = entry.CandlesElapsed + 1;
                _overlays[id] = entry with { CandlesElapsed = next };

                if (next >= entry.Signal.TtlCandles &&
                    entry.Signal.State is not (OverlayState.TargetHit or OverlayState.StopHit))
                {
                    toExpire.Add(id);
                }
            }
        }

        foreach (var id in toExpire)
            Transition(id, OverlayState.Expired);
    }

    private void OnIntraCandleUpdated(IntraCandleUpdateEvent e)
    {
        List<(int id, Signal signal)> toCheck = [];

        lock (_lock)
        {
            foreach (var (id, entry) in _overlays)
            {
                if (entry.Signal.Symbol    != e.Symbol    ||
                    entry.Signal.Timeframe != e.Timeframe) continue;

                if (entry.Signal.State is OverlayState.Pending or OverlayState.Active)
                    toCheck.Add((id, entry.Signal));
            }
        }

        foreach (var (id, signal) in toCheck)
        {
            switch (signal.State)
            {
                case OverlayState.Pending:
                    // Price enters entry zone
                    if (e.Candle.Low  <= signal.EntryHigh &&
                        e.Candle.High >= signal.EntryLow)
                        Transition(id, OverlayState.Active);
                    break;

                case OverlayState.Active:
                    if (e.Candle.High >= signal.TargetLow)
                        Transition(id, OverlayState.TargetHit);
                    else if (e.Candle.Low <= signal.StopPrice)
                        Transition(id, OverlayState.StopHit);
                    break;
            }
        }
    }

    // ── Transition ────────────────────────────────────────────────────────────

    private void Transition(int id, OverlayState newState)
    {
        Signal   signal;
        OverlayState oldState;

        lock (_lock)
        {
            if (!_overlays.TryGetValue(id, out var entry)) return;
            oldState = entry.Signal.State;
            if (oldState == newState) return;
            signal = entry.Signal with { State = newState };
            _overlays[id] = entry with { Signal = signal };
        }

        bool isTerminal = newState is OverlayState.TargetHit or
                                      OverlayState.StopHit   or
                                      OverlayState.Expired;

        var outcomeTime = isTerminal ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        _ = _store.UpdateStateAsync(id, newState, outcomeTime);

        // Record binary outcome for future kNN training
        if (newState is OverlayState.TargetHit)
            _ = _similarity.RecordOutcomeAsync(id, 1);
        else if (newState is OverlayState.StopHit)
            _ = _similarity.RecordOutcomeAsync(id, 0);

        _bus.Publish(new OverlayStateChangedEvent(id, signal.Symbol, oldState, newState));

        if (isTerminal)
            lock (_lock) { _overlays.Remove(id); }

        _logger.LogDebug("Signal {Id} {Old}→{New}", id, oldState, newState);
    }
}
