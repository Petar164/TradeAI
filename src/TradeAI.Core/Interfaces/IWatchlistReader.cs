using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>Read-only watchlist access. Used by Infrastructure so it does not need to reference Data.</summary>
public interface IWatchlistReader
{
    Task<List<WatchlistItem>> GetAllAsync();
}
