using Microsoft.Data.Sqlite;

namespace TradeAI.Data.Database;

/// <summary>
/// Manages the SQLite connection and schema initialization.
/// Injected as a singleton; each repository calls CreateConnection()
/// to get a fresh connection per operation (pooled automatically).
/// </summary>
public sealed class AppDbContext
{
    private readonly string _connectionString;

    public AppDbContext(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={databasePath}";
    }

    /// <summary>Opens and returns a new pooled SQLite connection.</summary>
    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Creates all tables and indexes if they don't exist.
    /// Also enables WAL mode. Called once on app startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // WAL mode — allows concurrent reads during writes
        await ExecAsync(conn, "PRAGMA journal_mode=WAL;");
        await ExecAsync(conn, "PRAGMA foreign_keys=ON;");

        foreach (var sql in SchemaStatements)
            await ExecAsync(conn, sql);
    }

    // ── Settings helpers (key/value store used throughout the app) ──────────

    public async Task<string?> GetSettingAsync(string key)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static async Task ExecAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Schema ───────────────────────────────────────────────────────────────

    private static readonly string[] SchemaStatements =
    [
        // Candles
        """
        CREATE TABLE IF NOT EXISTS candles (
            id          INTEGER PRIMARY KEY,
            symbol      TEXT    NOT NULL,
            timeframe   TEXT    NOT NULL,
            open_time   INTEGER NOT NULL,
            open        REAL    NOT NULL,
            high        REAL    NOT NULL,
            low         REAL    NOT NULL,
            close       REAL    NOT NULL,
            volume      REAL    NOT NULL,
            UNIQUE(symbol, timeframe, open_time)
        )
        """,

        "CREATE INDEX IF NOT EXISTS idx_candles_symbol_tf ON candles(symbol, timeframe, open_time DESC)",

        // Signals
        """
        CREATE TABLE IF NOT EXISTS signals (
            id                        INTEGER PRIMARY KEY,
            symbol                    TEXT    NOT NULL,
            timeframe                 TEXT    NOT NULL,
            signal_type               TEXT    NOT NULL,
            direction                 TEXT    NOT NULL,
            detected_at_candle_time   INTEGER NOT NULL,
            entry_low                 REAL    NOT NULL,
            entry_high                REAL    NOT NULL,
            stop_price                REAL    NOT NULL,
            target_low                REAL    NOT NULL,
            target_high               REAL    NOT NULL,
            ttl_candles               INTEGER NOT NULL,
            confidence_pct            REAL,
            similarity_sample_count   INTEGER,
            historical_hit_rate_pct   REAL,
            state                     TEXT    NOT NULL,
            outcome_time              INTEGER,
            feature_vector_json       TEXT
        )
        """,

        // Feature vectors (kNN store)
        """
        CREATE TABLE IF NOT EXISTS feature_vectors (
            id           INTEGER PRIMARY KEY,
            signal_id    INTEGER REFERENCES signals(id),
            signal_type  TEXT    NOT NULL,
            vector_json  TEXT    NOT NULL,
            outcome      INTEGER,
            created_at   INTEGER NOT NULL
        )
        """,

        "CREATE INDEX IF NOT EXISTS idx_fv_signal_type ON feature_vectors(signal_type, outcome)",

        // Watchlist
        """
        CREATE TABLE IF NOT EXISTS watchlist (
            id         INTEGER PRIMARY KEY,
            symbol     TEXT    NOT NULL UNIQUE,
            asset_type TEXT    NOT NULL,
            position   INTEGER NOT NULL,
            is_active  INTEGER NOT NULL DEFAULT 1
        )
        """,

        // Risk profiles
        """
        CREATE TABLE IF NOT EXISTS risk_profiles (
            id                    INTEGER PRIMARY KEY,
            created_at            INTEGER NOT NULL,
            max_risk_pct          REAL    NOT NULL,
            stop_style            TEXT    NOT NULL,
            max_concurrent_trades INTEGER NOT NULL,
            drawdown_tolerance    TEXT    NOT NULL,
            is_active             INTEGER NOT NULL DEFAULT 0
        )
        """,

        // App settings (key/value)
        """
        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        )
        """
    ];
}
