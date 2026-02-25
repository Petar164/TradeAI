using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.MarketData;

public sealed class YahooFinanceProvider : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceProvider> _logger;

    // Yahoo v8 crumb auth (required since late 2024)
    private string? _crumb;
    private readonly SemaphoreSlim _authLock = new(1, 1);

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

        var handler = new HttpClientHandler
        {
            CookieContainer          = new CookieContainer(),
            AutomaticDecompression   = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect        = true,
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/json,*/*");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<List<Candle>> GetHistoricalAsync(
        string symbol, string timeframe, int count, CancellationToken ct = default)
    {
        var (interval, range, _) = GetParams(timeframe);
        try
        {
            var json    = await FetchJsonAsync(symbol, interval, range, ct);
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
        try
        {
            var json    = await FetchJsonAsync(symbol, interval, partialRange, ct);
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

    // ── Auth (crumb) ──────────────────────────────────────────────────────────

    private async Task EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumb != null) return;

        await _authLock.WaitAsync(ct);
        try
        {
            if (_crumb != null) return;

            // Step 1: Visit Yahoo Finance and the consent gate to plant session
            // cookies. Both requests are best-effort; ignore failures.
            try { await _http.GetAsync("https://finance.yahoo.com", ct); }
            catch { /* cookie-seeding — ignore */ }
            try { await _http.GetAsync("https://fc.yahoo.com", ct); }
            catch { /* cookie-seeding — ignore */ }

            // Step 2: Fetch the crumb token (try query1 first, then query2)
            string crumb;
            try
            {
                crumb = await _http.GetStringAsync(
                    "https://query1.finance.yahoo.com/v1/test/getcrumb", ct);
            }
            catch
            {
                crumb = await _http.GetStringAsync(
                    "https://query2.finance.yahoo.com/v1/test/getcrumb", ct);
            }

            if (!string.IsNullOrWhiteSpace(crumb) && !crumb.StartsWith('{') && !crumb.StartsWith('<'))
            {
                _crumb = crumb.Trim();
                _logger.LogDebug("Yahoo crumb obtained.");
            }
            else
            {
                _logger.LogWarning("Yahoo crumb response unexpected: {Crumb}", crumb?[..Math.Min(80, crumb.Length)]);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to obtain Yahoo Finance crumb");
        }
        finally
        {
            _authLock.Release();
        }
    }

    // ── HTTP fetch (with crumb + 401 retry) ───────────────────────────────────

    private async Task<string> FetchJsonAsync(
        string symbol, string interval, string range, CancellationToken ct)
    {
        await EnsureCrumbAsync(ct);

        var url  = BuildUrl(symbol, interval, range);
        var resp = await _http.GetAsync(url, ct);

        // If unauthorized, refresh crumb once and retry
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Yahoo returned {Code} — refreshing crumb", resp.StatusCode);
            _crumb = null;
            await EnsureCrumbAsync(ct);
            url  = BuildUrl(symbol, interval, range);
            resp = await _http.GetAsync(url, ct);
        }

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildUrl(string symbol, string interval, string range)
    {
        var ys  = ToYahooSymbol(symbol);
        var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{ys}" +
                  $"?interval={interval}&range={range}&includePrePost=false";
        if (_crumb != null)
            url += $"&crumb={Uri.EscapeDataString(_crumb)}";
        return url;
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
        return parts[1].Equals("USD", StringComparison.OrdinalIgnoreCase) &&
               parts[0].Length == 3
            ? parts[0] + parts[1] + "=X"
            : parts[0] + "-" + parts[1];
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
            if (double.IsNaN(opens[i]) || double.IsNaN(closes[i])) continue;

            var ts     = DateTimeOffset.FromUnixTimeSeconds(tsList[i].GetInt64());
            var open   = opens[i];
            var high   = i < highs.Count   && !double.IsNaN(highs[i])   ? highs[i]   : Math.Max(open, closes[i]);
            var low    = i < lows.Count    && !double.IsNaN(lows[i])    ? lows[i]    : Math.Min(open, closes[i]);
            var close  = closes[i];
            var volume = i < volumes.Count ? (double)volumes[i] : 0.0;

            result.Add(new Candle(0, symbol, timeframe, ts, open, high, low, close, volume));
        }
        return result;
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
