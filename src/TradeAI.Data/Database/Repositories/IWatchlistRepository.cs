using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public interface IWatchlistRepository
{
    Task<List<WatchlistItem>> GetAllAsync();
    Task UpsertAsync(WatchlistItem item);
    Task RemoveAsync(string symbol);
    Task UpdatePositionAsync(string symbol, int position);
}
