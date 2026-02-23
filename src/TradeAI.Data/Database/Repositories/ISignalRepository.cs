using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public interface ISignalRepository : ISignalStore
{
    Task<List<Signal>> GetActiveAsync(string symbol);
    Task<List<Signal>> GetRecentAsync(string symbol, int limit = 50);
}
