namespace TradeAI.Core.Messaging.Events;

/// <summary>
/// Published by DataFeedManager after it successfully fetches and stores the
/// initial (or reloaded) candle history for a symbol/timeframe.
/// ChartViewModel listens for this to trigger a chart reload.
/// </summary>
public sealed record HistoricalDataReadyEvent(string Symbol, string Timeframe);
