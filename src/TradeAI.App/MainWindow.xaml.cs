using System.Windows;
using System.Windows.Controls;
using TradeAI.Infrastructure.MarketData;
using TradeAI.Infrastructure.Settings;
using TradeAI.UI.ViewModels;

namespace TradeAI.App;

public partial class MainWindow : Window
{
    private readonly ChartViewModel  _chartVm;
    private readonly DataFeedManager _feedManager;
    private readonly AppSettings     _settings;

    public MainWindow(ChartViewModel chartVm, DataFeedManager feedManager, AppSettings settings)
    {
        _chartVm     = chartVm;
        _feedManager = feedManager;
        _settings    = settings;

        InitializeComponent();

        // Wire the chart ViewModel into the ChartView UserControl
        ChartPanel.DataContext = _chartVm;

        SizeToWorkArea();
        SetActiveTimeframe(_settings.ActiveTimeframe);
    }

    // ── Window sizing ──────────────────────────────────────────────────────────
    private void SizeToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        Width  = wa.Width  * 0.88;
        Height = wa.Height * 0.90;
        Left   = wa.Left + (wa.Width  - Width)  / 2;
        Top    = wa.Top  + (wa.Height - Height) / 2;
    }

    // ── Window chrome ──────────────────────────────────────────────────────────
    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    // ── Timeframe selector ────────────────────────────────────────────────────
    private void TfButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tf) return;
        SetActiveTimeframe(tf);
        _feedManager.SetActiveSymbol(_settings.ActiveSymbol, tf);
        _ = _chartVm.ChangeTimeframeAsync(tf);
    }

    private void SetActiveTimeframe(string tf)
    {
        foreach (var child in TfButtonPanel.Children)
        {
            if (child is Button btn)
                btn.Style = (btn.Tag as string) == tf
                    ? (Style)FindResource("TimeframeButtonActiveStyle")
                    : (Style)FindResource("TimeframeButtonStyle");
        }
    }
}
