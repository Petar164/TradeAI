using TradeAI.Core.Models;

namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired when a signal detector produces a new trade setup.</summary>
public sealed record SignalDetectedEvent(
    Signal Signal);
