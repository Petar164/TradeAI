using TradeAI.Core.Models;

namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired when the user changes their risk/position-sizing profile.</summary>
public sealed record RiskProfileUpdatedEvent(
    RiskProfile Profile);
