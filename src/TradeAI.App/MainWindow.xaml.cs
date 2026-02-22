using System.Windows;
using System.Windows.Controls;

namespace TradeAI.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SizeToWorkArea();
        SetActiveTimeframe("5m");
    }

    // Size window to 88% of the current work area so it always fits on screen
    private void SizeToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        Width  = wa.Width  * 0.88;
        Height = wa.Height * 0.90;
        Left   = wa.Left + (wa.Width  - Width)  / 2;
        Top    = wa.Top  + (wa.Height - Height) / 2;
    }

    // ─── Window chrome ─────────────────────────────────────────
    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    // ─── Timeframe selector ────────────────────────────────────
    private void TfButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tf)
            SetActiveTimeframe(tf);
    }

    private void SetActiveTimeframe(string tf)
    {
        foreach (var child in TfButtonPanel.Children)
        {
            if (child is Button btn)
            {
                btn.Style = (btn.Tag as string) == tf
                    ? (Style)FindResource("TimeframeButtonActiveStyle")
                    : (Style)FindResource("TimeframeButtonStyle");
            }
        }
    }
}
