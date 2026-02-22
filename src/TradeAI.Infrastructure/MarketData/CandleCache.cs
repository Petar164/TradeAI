using System.Collections.Concurrent;
using TradeAI.Core.Models;
using TradeAI.Infrastructure.Settings;

namespace TradeAI.Infrastructure.MarketData;

/// <summary>
/// Thread-safe in-memory candle store (oldest-first per symbol/timeframe key).
/// Max <see cref="AppSettings.CandleCacheDepth"/> candles kept per key.
/// </summary>
public sealed class CandleCache
{
    private readonly int _maxDepth;
    private readonly ConcurrentDictionary<(string Symbol, string Timeframe), List<Candle>> _store = new();

    public CandleCache(AppSettings settings) => _maxDepth = settings.CandleCacheDepth;

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Replace the entire list for a key (trims to max depth, oldest-first).</summary>
    public void Set(string symbol, string timeframe, List<Candle> candles)
    {
        var list = candles.Count > _maxDepth
            ? candles.GetRange(candles.Count - _maxDepth, _maxDepth)
            : new List<Candle>(candles);
        _store[(symbol, timeframe)] = list;
    }

    /// <summary>
    /// If the last candle in the cache has the same OpenTime as <paramref name="candle"/>, replace it (intra-candle update).
    /// Otherwise append as a new candle (candle closed, new one started), trimming overflow.
    /// </summary>
    public void UpdateLastOrAppend(string symbol, string timeframe, Candle candle)
    {
        var list = _store.GetOrAdd((symbol, timeframe), _ => []);
        lock (list)
        {
            if (list.Count > 0 && list[^1].OpenTime == candle.OpenTime)
                list[^1] = candle;
            else
            {
                list.Add(candle);
                if (list.Count > _maxDepth)
                    list.RemoveAt(0);
            }
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Returns the most recent candle, or null if the key is not cached.</summary>
    public Candle? GetLast(string symbol, string timeframe)
    {
        if (!_store.TryGetValue((symbol, timeframe), out var list)) return null;
        lock (list) return list.Count > 0 ? list[^1] : null;
    }

    /// <summary>Returns a snapshot of the most recent <paramref name="count"/> candles, oldest-first.</summary>
    public List<Candle>? TryGet(string symbol, string timeframe, int count = 200)
    {
        if (!_store.TryGetValue((symbol, timeframe), out var list)) return null;
        lock (list)
        {
            var take = Math.Min(count, list.Count);
            return list.GetRange(list.Count - take, take);
        }
    }

    public bool HasData(string symbol, string timeframe) =>
        _store.TryGetValue((symbol, timeframe), out var l) && l.Count > 0;
}
