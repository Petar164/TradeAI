using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public interface ICandleRepository
{
    Task UpsertAsync(Candle candle);
    Task UpsertBatchAsync(IEnumerable<Candle> candles);
    Task<List<Candle>> GetRecentAsync(string symbol, string timeframe, int count);
}
