using Microsoft.Data.Sqlite;
using TradeAI.Core.Models;

namespace TradeAI.Data.Database.Repositories;

public sealed class SignalRepository : ISignalRepository
{
    private readonly AppDbContext _db;

    public SignalRepository(AppDbContext db) => _db = db;

    public async Task<int> InsertAsync(Signal signal)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO signals
                (symbol, timeframe, signal_type, direction, detected_at_candle_time,
                 entry_low, entry_high, stop_price, target_low, target_high, ttl_candles,
                 confidence_pct, similarity_sample_count, historical_hit_rate_pct,
                 state, outcome_time, feature_vector_json)
            VALUES
                (@symbol, @timeframe, @signal_type, @direction, @detected_at,
                 @entry_low, @entry_high, @stop_price, @target_low, @target_high, @ttl,
                 @confidence, @similarity_count, @hit_rate,
                 @state, @outcome_time, @fv_json);
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@symbol",           signal.Symbol);
        cmd.Parameters.AddWithValue("@timeframe",        signal.Timeframe);
        cmd.Parameters.AddWithValue("@signal_type",      signal.SignalType);
        cmd.Parameters.AddWithValue("@direction",        signal.Direction.ToString());
        cmd.Parameters.AddWithValue("@detected_at",      signal.DetectedAtCandleTime.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("@entry_low",        signal.EntryLow);
        cmd.Parameters.AddWithValue("@entry_high",       signal.EntryHigh);
        cmd.Parameters.AddWithValue("@stop_price",       signal.StopPrice);
        cmd.Parameters.AddWithValue("@target_low",       signal.TargetLow);
        cmd.Parameters.AddWithValue("@target_high",      signal.TargetHigh);
        cmd.Parameters.AddWithValue("@ttl",              signal.TtlCandles);
        cmd.Parameters.AddWithValue("@confidence",       (object?)signal.ConfidencePct        ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@similarity_count", (object?)signal.SimilaritySampleCount ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@hit_rate",         (object?)signal.HistoricalHitRatePct  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@state",            signal.State.ToString());
        cmd.Parameters.AddWithValue("@outcome_time",     signal.OutcomeTime.HasValue
                                                            ? (object)signal.OutcomeTime.Value.ToUnixTimeMilliseconds()
                                                            : DBNull.Value);
        cmd.Parameters.AddWithValue("@fv_json",          (object?)signal.FeatureVectorJson ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(id);
    }

    public async Task UpdateStateAsync(int id, OverlayState state, DateTimeOffset? outcomeTime = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE signals
            SET state = @state, outcome_time = @outcome_time
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@state",        state.ToString());
        cmd.Parameters.AddWithValue("@outcome_time", outcomeTime.HasValue
                                                        ? (object)outcomeTime.Value.ToUnixTimeMilliseconds()
                                                        : DBNull.Value);
        cmd.Parameters.AddWithValue("@id",           id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Signal>> GetActiveAsync(string symbol)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM signals
            WHERE symbol = @symbol AND state IN ('Pending','Active')
            ORDER BY detected_at_candle_time DESC
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        return await ReadSignals(cmd);
    }

    public async Task<List<Signal>> GetRecentAsync(string symbol, int limit = 50)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM signals
            WHERE symbol = @symbol
            ORDER BY detected_at_candle_time DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@symbol", symbol);
        cmd.Parameters.AddWithValue("@limit",  limit);
        return await ReadSignals(cmd);
    }

    private static async Task<List<Signal>> ReadSignals(SqliteCommand cmd)
    {
        var result = new List<Signal>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new Signal
            {
                Id                    = r.GetInt32(r.GetOrdinal("id")),
                Symbol                = r.GetString(r.GetOrdinal("symbol")),
                Timeframe             = r.GetString(r.GetOrdinal("timeframe")),
                SignalType            = r.GetString(r.GetOrdinal("signal_type")),
                Direction             = Enum.Parse<TradeDirection>(r.GetString(r.GetOrdinal("direction"))),
                DetectedAtCandleTime  = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(r.GetOrdinal("detected_at_candle_time"))),
                EntryLow              = r.GetDouble(r.GetOrdinal("entry_low")),
                EntryHigh             = r.GetDouble(r.GetOrdinal("entry_high")),
                StopPrice             = r.GetDouble(r.GetOrdinal("stop_price")),
                TargetLow             = r.GetDouble(r.GetOrdinal("target_low")),
                TargetHigh            = r.GetDouble(r.GetOrdinal("target_high")),
                TtlCandles            = r.GetInt32(r.GetOrdinal("ttl_candles")),
                ConfidencePct         = r.IsDBNull(r.GetOrdinal("confidence_pct"))           ? null : r.GetDouble(r.GetOrdinal("confidence_pct")),
                SimilaritySampleCount = r.IsDBNull(r.GetOrdinal("similarity_sample_count"))  ? null : r.GetInt32(r.GetOrdinal("similarity_sample_count")),
                HistoricalHitRatePct  = r.IsDBNull(r.GetOrdinal("historical_hit_rate_pct"))  ? null : r.GetDouble(r.GetOrdinal("historical_hit_rate_pct")),
                State                 = Enum.Parse<OverlayState>(r.GetString(r.GetOrdinal("state"))),
                OutcomeTime           = r.IsDBNull(r.GetOrdinal("outcome_time"))             ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(r.GetOrdinal("outcome_time"))),
                FeatureVectorJson     = r.IsDBNull(r.GetOrdinal("feature_vector_json"))      ? null : r.GetString(r.GetOrdinal("feature_vector_json")),
            });
        }
        return result;
    }
}
