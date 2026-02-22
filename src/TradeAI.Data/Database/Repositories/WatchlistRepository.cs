using Microsoft.Data.Sqlite;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public sealed class WatchlistRepository : IWatchlistRepository
{
    private readonly AppDbContext _db;

    public WatchlistRepository(AppDbContext db) => _db = db;

    public async Task<List<WatchlistItem>> GetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, symbol, asset_type, position, is_active
            FROM watchlist
            WHERE is_active = 1
            ORDER BY position ASC
            """;

        var result = new List<WatchlistItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new WatchlistItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4) == 1));
        }
        return result;
    }

    public async Task UpsertAsync(WatchlistItem item)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO watchlist (symbol, asset_type, position, is_active)
            VALUES (@symbol, @asset_type, @position, @is_active)
            ON CONFLICT(symbol) DO UPDATE SET
                asset_type = excluded.asset_type,
                position   = excluded.position,
                is_active  = excluded.is_active
            """;
        cmd.Parameters.AddWithValue("@symbol",     item.Symbol);
        cmd.Parameters.AddWithValue("@asset_type", item.AssetType);
        cmd.Parameters.AddWithValue("@position",   item.Position);
        cmd.Parameters.AddWithValue("@is_active",  item.IsActive ? 1 : 0);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveAsync(string symbol)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Soft delete — keeps historical signal data intact
        cmd.CommandText = "UPDATE watchlist SET is_active = 0 WHERE symbol = @symbol";
        cmd.Parameters.AddWithValue("@symbol", symbol);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePositionAsync(string symbol, int position)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE watchlist SET position = @position WHERE symbol = @symbol";
        cmd.Parameters.AddWithValue("@position", position);
        cmd.Parameters.AddWithValue("@symbol",   symbol);
        await cmd.ExecuteNonQueryAsync();
    }
}
