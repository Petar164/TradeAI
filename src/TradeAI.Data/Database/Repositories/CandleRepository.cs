using Microsoft.Data.Sqlite;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public sealed class CandleRepository : ICandleRepository
{
    private readonly AppDbContext _db;

    public CandleRepository(AppDbContext db) => _db = db;

    public async Task UpsertAsync(Candle candle)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO candles
                (symbol, timeframe, open_time, open, high, low, close, volume)
            VALUES
                (@symbol, @timeframe, @open_time, @open, @high, @low, @close, @volume)
            """;
        BindCandle(cmd, candle);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpsertBatchAsync(IEnumerable<Candle> candles)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT OR REPLACE INTO candles
                (symbol, timeframe, open_time, open, high, low, close, volume)
            VALUES
                (@symbol, @timeframe, @open_time, @open, @high, @low, @close, @volume)
            """;

        // Pre-create parameters once, reuse per row
        var pSymbol    = cmd.Parameters.Add("@symbol",    SqliteType.Text);
        var pTimeframe = cmd.Parameters.Add("@timeframe", SqliteType.Text);
        var pOpenTime  = cmd.Parameters.Add("@open_time", SqliteType.Integer);
        var pOpen      = cmd.Parameters.Add("@open",      SqliteType.Real);
        var pHigh      = cmd.Parameters.Add("@high",      SqliteType.Real);
        var pLow       = cmd.Parameters.Add("@low",       SqliteType.Real);
        var pClose     = cmd.Parameters.Add("@close",     SqliteType.Real);
        var pVolume    = cmd.Parameters.Add("@volume",    SqliteType.Real);

        foreach (var c in candles)
        {
            pSymbol.Value    = c.Symbol;
            pTimeframe.Value = c.Timeframe;
            pOpenTime.Value  = c.OpenTime.ToUnixTimeMilliseconds();
            pOpen.Value      = c.Open;
            pHigh.Value      = c.High;
            pLow.Value       = c.Low;
            pClose.Value     = c.Close;
            pVolume.Value    = c.Volume;
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task<List<Candle>> GetRecentAsync(string symbol, string timeframe, int count)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, symbol, timeframe, open_time, open, high, low, close, volume
            FROM candles
            WHERE symbol = @symbol AND timeframe = @timeframe
            ORDER BY open_time DESC
            LIMIT @count
            """;
        cmd.Parameters.AddWithValue("@symbol",    symbol);
        cmd.Parameters.AddWithValue("@timeframe", timeframe);
        cmd.Parameters.AddWithValue("@count",     count);

        var result = new List<Candle>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Candle(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7),
                reader.GetDouble(8)));
        }

        result.Reverse(); // Return oldest-first
        return result;
    }

    private static void BindCandle(SqliteCommand cmd, Candle c)
    {
        cmd.Parameters.AddWithValue("@symbol",    c.Symbol);
        cmd.Parameters.AddWithValue("@timeframe", c.Timeframe);
        cmd.Parameters.AddWithValue("@open_time", c.OpenTime.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("@open",      c.Open);
        cmd.Parameters.AddWithValue("@high",      c.High);
        cmd.Parameters.AddWithValue("@low",       c.Low);
        cmd.Parameters.AddWithValue("@close",     c.Close);
        cmd.Parameters.AddWithValue("@volume",    c.Volume);
    }
}
