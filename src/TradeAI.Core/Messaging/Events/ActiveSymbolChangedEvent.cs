namespace TradeAI.Core.Messaging.Events;

/// <summary>Fired when the user switches the active chart symbol or timeframe.</summary>
public sealed record ActiveSymbolChangedEvent(
    string Symbol,
    string Timeframe);
