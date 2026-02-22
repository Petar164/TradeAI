using TradeAI.Core.Models;

namespace TradeAI.Core.Events;

public sealed class CandleEventArgs : EventArgs
{
    public required string Symbol    { get; init; }
    public required string Timeframe { get; init; }
    public required Candle Candle    { get; init; }
}
