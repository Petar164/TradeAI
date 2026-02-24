using Microsoft.Data.Sqlite;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;
using TradeAI.Data.Database;

namespace TradeAI.Data.Database.Repositories;

/// <summary>
/// SQLite-backed implementation of <see cref="IRiskProfileRepository"/>.
/// Stores one active profile at a time; saving a new profile deactivates all others.
/// </summary>
public sealed class RiskProfileRepository : IRiskProfileRepository
{
    private readonly AppDbContext _db;

    public RiskProfileRepository(AppDbContext db) => _db = db;

    public async Task<RiskProfile?> GetActiveAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, created_at, max_risk_pct, stop_style,
                   max_concurrent_trades, drawdown_tolerance
            FROM   risk_profiles
            WHERE  is_active = 1
            ORDER  BY id DESC
            LIMIT  1;
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new RiskProfile
        {
            Id                  = reader.GetInt32(0),
            CreatedAt           = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
            MaxRiskPct          = reader.GetDouble(2),
            StopStyle           = Enum.Parse<StopStyle>(reader.GetString(3), ignoreCase: true),
            MaxConcurrentTrades = reader.GetInt32(4),
            DrawdownTolerance   = Enum.Parse<DrawdownTolerance>(reader.GetString(5), ignoreCase: true),
            IsActive            = true,
        };
    }

    public async Task SaveAsync(RiskProfile profile)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        // Deactivate all existing profiles
        await using var deactivate = conn.CreateCommand();
        deactivate.CommandText = "UPDATE risk_profiles SET is_active = 0;";
        await deactivate.ExecuteNonQueryAsync();

        // Insert new active profile
        await using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO risk_profiles
                (created_at, max_risk_pct, stop_style,
                 max_concurrent_trades, drawdown_tolerance, is_active)
            VALUES
                (@ca, @risk, @stop, @conc, @dd, 1);
            """;
        insert.Parameters.AddWithValue("@ca",   DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        insert.Parameters.AddWithValue("@risk",  profile.MaxRiskPct);
        insert.Parameters.AddWithValue("@stop",  profile.StopStyle.ToString());
        insert.Parameters.AddWithValue("@conc",  profile.MaxConcurrentTrades);
        insert.Parameters.AddWithValue("@dd",    profile.DrawdownTolerance.ToString());
        await insert.ExecuteNonQueryAsync();
    }
}
