using TradeAI.Core.Models;

namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired on every live tick while a candle is still forming.</summary>
public sealed record IntraCandleUpdateEvent(
    string Symbol,
    string Timeframe,
    Candle Candle);
