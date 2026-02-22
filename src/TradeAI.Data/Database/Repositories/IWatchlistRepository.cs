using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

// Extends IWatchlistReader so Infrastructure can depend on IWatchlistReader (Core) without referencing Data.
public interface IWatchlistRepository : IWatchlistReader
{
    Task UpsertAsync(WatchlistItem item);
    Task RemoveAsync(string symbol);
    Task UpdatePositionAsync(string symbol, int position);
}
