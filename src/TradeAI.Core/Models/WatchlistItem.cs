namespace TradeAI.Core.Models;

/// <summary>One symbol tracked on the watchlist.</summary>
public record WatchlistItem(
    int      Id,
    string   Symbol,
    string   AssetType,   // "STOCK" | "FOREX"
    int      Position,    // display order
    bool     IsActive
);
