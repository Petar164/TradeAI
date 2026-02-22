using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.MarketData;

public sealed class YahooFinanceProvider : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceProvider> _logger;

    // Yahoo interval + historical range per app timeframe
    private static readonly Dictionary<string, (string Interval, string HistRange, string PartialRange)> TfMap = new()
    {
        ["1m"]  = ("1m",  "1d",  "1d"),
        ["3m"]  = ("5m",  "5d",  "1d"),   // Yahoo has no 3m
        ["5m"]  = ("5m",  "5d",  "1d"),
        ["15m"] = ("15m", "60d", "5d"),
        ["30m"] = ("30m", "60d", "5d"),
        ["1h"]  = ("60m", "730d","60d"),
        ["1d"]  = ("1d",  "5y",  "1mo"),
    };

    public YahooFinanceProvider(ILogger<YahooFinanceProvider> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<List<Candle>> GetHistoricalAsync(
        string symbol, string timeframe, int count, CancellationToken ct = default)
    {
        var (interval, range, _) = GetParams(timeframe);
        var url = BuildUrl(symbol, interval, range);
        try
        {
            var json    = await _http.GetStringAsync(url, ct);
            var candles = ParseCandles(json, symbol, timeframe);
            if (candles.Count > count)
                candles = candles.GetRange(candles.Count - count, count);
            return candles;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo historical fetch failed for {Symbol}/{Timeframe}", symbol, timeframe);
            return [];
        }
    }

    public async Task<Candle?> GetLatestPartialAsync(
        string symbol, string timeframe, CancellationToken ct = default)
    {
        var (interval, _, partialRange) = GetParams(timeframe);
        var url = BuildUrl(symbol, interval, partialRange);
        try
        {
            var json    = await _http.GetStringAsync(url, ct);
            var candles = ParseCandles(json, symbol, timeframe);
            return candles.Count > 0 ? candles[^1] : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo partial fetch failed for {Symbol}/{Timeframe}", symbol, timeframe);
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildUrl(string symbol, string interval, string range)
    {
        var ys = ToYahooSymbol(symbol);
        return $"https://query1.finance.yahoo.com/v8/finance/chart/{ys}" +
               $"?interval={interval}&range={range}&includePrePost=false";
    }

    private static (string Interval, string HistRange, string PartialRange) GetParams(string tf) =>
        TfMap.TryGetValue(tf, out var p) ? p : ("5m", "5d", "1d");

    /// <summary>
    /// EUR/USD → EURUSD=X  (forex)
    /// BTC/USD → BTC-USD   (crypto, future)
    /// AAPL    → AAPL      (stock, no change)
    /// </summary>
    private static string ToYahooSymbol(string symbol)
    {
        if (!symbol.Contains('/')) return symbol;
        var parts = symbol.Split('/');
        // Treat anything quoted in USD that starts with a 3-letter currency as forex
        return parts[1].Equals("USD", StringComparison.OrdinalIgnoreCase) &&
               parts[0].Length == 3
            ? parts[0] + parts[1] + "=X"
            : parts[0] + "-" + parts[1];   // crypto fallback
    }

    private static List<Candle> ParseCandles(string json, string symbol, string timeframe)
    {
        var result = new List<Candle>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("chart", out var chart)) return result;
        if (!chart.TryGetProperty("result", out var results))         return result;
        if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return result;

        var r = results[0];
        if (!r.TryGetProperty("timestamp", out var tsEl))   return result;
        if (!r.TryGetProperty("indicators", out var indEl)) return result;
        if (!indEl.TryGetProperty("quote", out var qArr))   return result;
        if (qArr.GetArrayLength() == 0)                     return result;

        var q       = qArr[0];
        var tsList  = tsEl.EnumerateArray().ToList();
        var opens   = GetDoubleArray(q, "open");
        var highs   = GetDoubleArray(q, "high");
        var lows    = GetDoubleArray(q, "low");
        var closes  = GetDoubleArray(q, "close");
        var volumes = GetLongArray(q, "volume");

        for (int i = 0; i < tsList.Count; i++)
        {
            if (i >= opens.Count || i >= closes.Count) break;
            if (double.IsNaN(opens[i]) || double.IsNaN(closes[i])) continue;  // market gap

            var ts     = DateTimeOffset.FromUnixTimeSeconds(tsList[i].GetInt64());
            var open   = opens[i];
            var high   = i < highs.Count   && !double.IsNaN(highs[i])   ? highs[i]   : Math.Max(open, closes[i]);
            var low    = i < lows.Count    && !double.IsNaN(lows[i])    ? lows[i]    : Math.Min(open, closes[i]);
            var close  = closes[i];
            var volume = i < volumes.Count ? (double)volumes[i] : 0.0;

            result.Add(new Candle(0, symbol, timeframe, ts, open, high, low, close, volume));
        }
        return result;   // already oldest-first from Yahoo
    }

    private static List<double> GetDoubleArray(JsonElement parent, string key)
    {
        var list = new List<double>();
        if (!parent.TryGetProperty(key, out var el)) return list;
        foreach (var item in el.EnumerateArray())
            list.Add(item.ValueKind == JsonValueKind.Null ? double.NaN : item.GetDouble());
        return list;
    }

    private static List<long> GetLongArray(JsonElement parent, string key)
    {
        var list = new List<long>();
        if (!parent.TryGetProperty(key, out var el)) return list;
        foreach (var item in el.EnumerateArray())
            list.Add(item.ValueKind == JsonValueKind.Null ? 0L : item.GetInt64());
        return list;
    }
}
