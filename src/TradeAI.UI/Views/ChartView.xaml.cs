using System.Windows.Controls;
using TradeAI.UI.ViewModels;

namespace TradeAI.UI.Views;

public partial class ChartView : UserControl
{
    public ChartView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ChartViewModel vm)
                await vm.InitializeChartAsync(WebView);
        };
    }
}
