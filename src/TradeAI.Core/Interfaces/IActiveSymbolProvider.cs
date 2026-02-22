namespace TradeAI.Core.Interfaces;

/// <summary>
/// Provides and mutates the currently active chart symbol and timeframe.
/// Implemented by AppSettings. Injected into UI layer so UI does not reference Infrastructure.
/// </summary>
public interface IActiveSymbolProvider
{
    string ActiveSymbol    { get; set; }
    string ActiveTimeframe { get; set; }
}
