using TradeAI.Core.Models;

namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired whenever an active overlay transitions to a new state.</summary>
public sealed record OverlayStateChangedEvent(
    int          SignalId,
    string       Symbol,
    OverlayState PreviousState,
    OverlayState NewState);
