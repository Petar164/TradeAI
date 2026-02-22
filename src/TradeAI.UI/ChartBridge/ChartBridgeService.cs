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

        // Load chart-host.html from this assembly's embedded resources
        var asm  = typeof(ChartBridgeService).Assembly;
        var name = "TradeAI.UI.ChartBridge.chart-host.html";
        await using var stream = asm.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded resource not found: {name}");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
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
