using Microsoft.Data.Sqlite;

namespace TradeAI.Data.Database.Repositories;

public sealed class FeatureVectorRepository : IFeatureVectorRepository
{
    private readonly AppDbContext _db;

    public FeatureVectorRepository(AppDbContext db) => _db = db;

    public async Task InsertAsync(int signalId, string signalType, string vectorJson, int? outcome = null)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO feature_vectors (signal_id, signal_type, vector_json, outcome, created_at)
            VALUES (@signal_id, @signal_type, @vector_json, @outcome, @created_at)
            """;
        cmd.Parameters.AddWithValue("@signal_id",   signalId);
        cmd.Parameters.AddWithValue("@signal_type", signalType);
        cmd.Parameters.AddWithValue("@vector_json", vectorJson);
        cmd.Parameters.AddWithValue("@outcome",     (object?)outcome ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at",  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RecordOutcomeAsync(int signalId, int outcome)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE feature_vectors SET outcome = @outcome WHERE signal_id = @signal_id";
        cmd.Parameters.AddWithValue("@outcome",   outcome);
        cmd.Parameters.AddWithValue("@signal_id", signalId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string VectorJson, int? Outcome)>> GetByTypeAsync(string signalType)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT vector_json, outcome
            FROM feature_vectors
            WHERE signal_type = @signal_type
            ORDER BY created_at DESC
            LIMIT 500000
            """;
        cmd.Parameters.AddWithValue("@signal_type", signalType);

        var result = new List<(string, int?)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json    = reader.GetString(0);
            var outcome = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            result.Add((json, outcome));
        }
        return result;
    }
}
