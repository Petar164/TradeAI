using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using TradeAI.Core.Models;

namespace TradeAI.UI.ChartBridge;

/// <summary>
/// Owns the WebView2 ↔ JavaScript bridge.
/// All public methods are safe to call from the UI thread only.
/// </summary>
public sealed class ChartBridgeService
{
    private WebView2?                      _webView;
    private TaskCompletionSource<bool>?    _readyTcs;
    private readonly ILogger<ChartBridgeService> _logger;

    public bool IsReady { get; private set; }

    public ChartBridgeService(ILogger<ChartBridgeService> logger) => _logger = logger;

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Call once from the UI thread after the WebView2 control is loaded.
    /// Navigates to chart-host.html and awaits the CHART_READY message.
    /// </summary>
    public async Task InitializeAsync(WebView2 webView)
    {
        IsReady   = false;
        _webView  = webView;
        _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        // Load chart-host.html and inline the bundled LightweightCharts script
        var asm = typeof(ChartBridgeService).Assembly;

        await using var htmlStream = asm.GetManifestResourceStream("TradeAI.UI.ChartBridge.chart-host.html")
            ?? throw new FileNotFoundException("Embedded resource not found: chart-host.html");
        var html = await new StreamReader(htmlStream).ReadToEndAsync();

        await using var jsStream = asm.GetManifestResourceStream(
                "TradeAI.UI.ChartBridge.lightweight-charts.standalone.production.js")
            ?? throw new FileNotFoundException("Embedded resource not found: lightweight-charts.js");
        var js = await new StreamReader(jsStream).ReadToEndAsync();

        html = html.Replace("<!-- __LIGHTWEIGHT_CHARTS_SCRIPT__ -->", $"<script>{js}</script>");
        webView.NavigateToString(html);

        // Wait up to 30 s for CHART_READY
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => _readyTcs.TrySetCanceled(), useSynchronizationContext: false);
        try
        {
            await _readyTcs.Task;
            IsReady = true;
            _logger.LogInformation("ChartBridge ready.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ChartBridge timed out waiting for CHART_READY.");
        }
    }

    // ── Chart commands ────────────────────────────────────────────────────────

    /// <summary>Replace all candle data on the chart (oldest-first list).</summary>
    public async Task LoadCandlesAsync(List<Candle> candles)
    {
        if (!IsReady || _webView == null) return;

        var data = candles.Select(c => new
        {
            time  = c.OpenTime.ToUnixTimeSeconds(),
            open  = c.Open,
            high  = c.High,
            low   = c.Low,
            close = c.Close,
        });
        var json = JsonSerializer.Serialize(data);
        await _webView.ExecuteScriptAsync($"window.bridge.loadCandles({json})");
    }

    /// <summary>Update or append the current (partial) candle.</summary>
    public async Task UpdateLastCandleAsync(Candle candle)
    {
        if (!IsReady || _webView == null) return;

        var data = new
        {
            time  = candle.OpenTime.ToUnixTimeSeconds(),
            open  = candle.Open,
            high  = candle.High,
            low   = candle.Low,
            close = candle.Close,
        };
        var json = JsonSerializer.Serialize(data);
        await _webView.ExecuteScriptAsync($"window.bridge.updateLastCandle({json})");
    }

    // ── Signal overlay commands ───────────────────────────────────────────────

    /// <summary>Serialize signal and call window.bridge.drawSignal().</summary>
    public async Task DrawSignalAsync(Signal signal)
    {
        if (!IsReady || _webView == null) return;

        var data = new
        {
            id               = signal.Id,
            signalType       = signal.SignalType,
            triggerTime      = signal.DetectedAtCandleTime.ToUnixTimeSeconds(),
            ttlCandles       = signal.TtlCandles,
            timeframeSeconds = TimeframeToSeconds(signal.Timeframe),
            entryLow         = signal.EntryLow,
            entryHigh        = signal.EntryHigh,
            stopPrice        = signal.StopPrice,
            targetLow        = signal.TargetLow,
            targetHigh       = signal.TargetHigh,
            direction        = signal.Direction.ToString(),
            confidencePct    = signal.ConfidencePct,
        };
        var json = JsonSerializer.Serialize(data);
        await _webView.ExecuteScriptAsync($"window.bridge.drawSignal({json})");
    }

    /// <summary>Call window.bridge.updateOverlayState(id, state).</summary>
    public async Task UpdateOverlayStateAsync(int signalId, OverlayState state)
    {
        if (!IsReady || _webView == null) return;
        await _webView.ExecuteScriptAsync(
            $"window.bridge.updateOverlayState({signalId}, '{state}')");
    }

    private static int TimeframeToSeconds(string tf) => tf switch
    {
        "1m"  => 60,
        "3m"  => 180,
        "5m"  => 300,
        "15m" => 900,
        "30m" => 1_800,
        "1h"  => 3_600,
        "4h"  => 14_400,
        "1d"  => 86_400,
        _     => 300,
    };

    // ── JS → C# messages ─────────────────────────────────────────────────────

    private void OnWebMessageReceived(object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc  = JsonDocument.Parse(e.WebMessageAsJson);
            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "CHART_READY")
                _readyTcs?.TrySetResult(true);
        }
        catch { /* ignore malformed messages */ }
    }
}
