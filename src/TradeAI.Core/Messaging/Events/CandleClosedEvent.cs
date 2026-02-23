using TradeAI.Core.Models;

namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired when a candle period closes and the bar is persisted.</summary>
public sealed record CandleClosedEvent(
    string Symbol,
    string Timeframe,
    Candle Candle);
