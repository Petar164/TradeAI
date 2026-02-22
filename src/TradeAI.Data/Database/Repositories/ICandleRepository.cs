using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

// Extends ICandleWriter so Infrastructure can depend on ICandleWriter (Core) without referencing Data.
public interface ICandleRepository : ICandleWriter
{
    Task<List<Candle>> GetRecentAsync(string symbol, string timeframe, int count);
}
