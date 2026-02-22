using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using TradeAI.Core.Events;
using TradeAI.Core.Interfaces;
using TradeAI.Data.Database.Repositories;
using TradeAI.UI.ChartBridge;

namespace TradeAI.UI.ViewModels;

public sealed class ChartViewModel
{
    private readonly ILiveCandleFeed         _feed;
    private readonly ChartBridgeService      _bridge;
    private readonly ICandleRepository       _candleRepo;
    private readonly IActiveSymbolProvider   _symbolProvider;
    private readonly ILogger<ChartViewModel> _logger;

    public ChartViewModel(
        ILiveCandleFeed          feed,
        ChartBridgeService       bridge,
        ICandleRepository        candleRepo,
        IActiveSymbolProvider    symbolProvider,
        ILogger<ChartViewModel>  logger)
    {
        _feed           = feed;
        _bridge         = bridge;
        _candleRepo     = candleRepo;
        _symbolProvider = symbolProvider;
        _logger         = logger;

        _feed.IntraCandleUpdated += OnIntraCandleUpdated;
        _feed.CandleClosed       += OnCandleClosed;
    }

    // ── Called from ChartView.xaml.cs (UI thread) ─────────────────────────────

    public async Task InitializeChartAsync(WebView2 webView)
    {
        await _bridge.InitializeAsync(webView);
        await ReloadCandlesAsync();
    }

    /// <summary>Switch timeframe — reloads chart candles from DB.</summary>
    public async Task ChangeTimeframeAsync(string timeframe)
    {
        _symbolProvider.ActiveTimeframe = timeframe;
        await ReloadCandlesAsync();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task ReloadCandlesAsync()
    {
        var candles = await _candleRepo.GetRecentAsync(
            _symbolProvider.ActiveSymbol, _symbolProvider.ActiveTimeframe, 200);
        if (candles.Count > 0)
            await _bridge.LoadCandlesAsync(candles);
    }

    private void OnIntraCandleUpdated(object? sender, CandleEventArgs e)
    {
        if (!_bridge.IsReady) return;
        if (e.Symbol != _symbolProvider.ActiveSymbol ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try   { await _bridge.UpdateLastCandleAsync(e.Candle); }
            catch (Exception ex)
            { _logger.LogWarning(ex, "IntraCandleUpdate dispatch error"); }
        });
    }

    private void OnCandleClosed(object? sender, CandleEventArgs e)
    {
        if (!_bridge.IsReady) return;
        if (e.Symbol != _symbolProvider.ActiveSymbol ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try   { await _bridge.UpdateLastCandleAsync(e.Candle); }
            catch (Exception ex)
            { _logger.LogWarning(ex, "CandleClosed dispatch error"); }
        });
    }
}
