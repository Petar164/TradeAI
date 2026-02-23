using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Data.Database.Repositories;
using TradeAI.UI.ChartBridge;

namespace TradeAI.UI.ViewModels;

public sealed class ChartViewModel
{
    private readonly SignalBus               _bus;
    private readonly ChartBridgeService      _bridge;
    private readonly ICandleRepository       _candleRepo;
    private readonly IActiveSymbolProvider   _symbolProvider;
    private readonly ILogger<ChartViewModel> _logger;

    // Strong references so SignalBus WeakReferences stay alive for the ViewModel lifetime
    private readonly Action<IntraCandleUpdateEvent> _onIntraCandle;
    private readonly Action<CandleClosedEvent>      _onCandleClosed;

    // Tokens — kept to allow future unsubscription (e.g. ViewModel teardown)
#pragma warning disable IDE0052
    private readonly IDisposable _intraSub;
    private readonly IDisposable _closedSub;
#pragma warning restore IDE0052

    public ChartViewModel(
        SignalBus                bus,
        ChartBridgeService       bridge,
        ICandleRepository        candleRepo,
        IActiveSymbolProvider    symbolProvider,
        ILogger<ChartViewModel>  logger)
    {
        _bus            = bus;
        _bridge         = bridge;
        _candleRepo     = candleRepo;
        _symbolProvider = symbolProvider;
        _logger         = logger;

        _onIntraCandle  = OnIntraCandleUpdated;
        _onCandleClosed = OnCandleClosed;

        _intraSub  = _bus.Subscribe<IntraCandleUpdateEvent>(_onIntraCandle);
        _closedSub = _bus.Subscribe<CandleClosedEvent>(_onCandleClosed);
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

    private void OnIntraCandleUpdated(IntraCandleUpdateEvent e)
    {
        if (!_bridge.IsReady) return;
        if (e.Symbol    != _symbolProvider.ActiveSymbol    ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try   { await _bridge.UpdateLastCandleAsync(e.Candle); }
            catch (Exception ex)
            { _logger.LogWarning(ex, "IntraCandleUpdate dispatch error"); }
        });
    }

    private void OnCandleClosed(CandleClosedEvent e)
    {
        if (!_bridge.IsReady) return;
        if (e.Symbol    != _symbolProvider.ActiveSymbol    ||
            e.Timeframe != _symbolProvider.ActiveTimeframe) return;

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try   { await _bridge.UpdateLastCandleAsync(e.Candle); }
            catch (Exception ex)
            { _logger.LogWarning(ex, "CandleClosed dispatch error"); }
        });
    }
}
