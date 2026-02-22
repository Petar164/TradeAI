using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public interface ISignalRepository
{
    Task<int> InsertAsync(Signal signal);
    Task UpdateStateAsync(int id, OverlayState state, DateTimeOffset? outcomeTime = null);
    Task<List<Signal>> GetActiveAsync(string symbol);
    Task<List<Signal>> GetRecentAsync(string symbol, int limit = 50);
}
