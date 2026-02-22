using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

public interface IMarketDataProvider
{
    /// <summary>Fetch historical OHLCV candles oldest-first. Returns empty list on failure.</summary>
    Task<List<Candle>> GetHistoricalAsync(string symbol, string timeframe, int count, CancellationToken ct = default);

    /// <summary>Fetch the current (potentially partial/live) candle. Returns null on failure.</summary>
    Task<Candle?> GetLatestPartialAsync(string symbol, string timeframe, CancellationToken ct = default);
}
