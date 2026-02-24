using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using TradeAI.Core.Models;

namespace TradeAI.App;

public partial class RiskProfileWizard : Window
{
    private int _step = 1;
    private const int TotalSteps = 4;

    /// <summary>
    /// The completed risk profile. Non-null only when DialogResult == true.
    /// </summary>
    public RiskProfile? Result { get; private set; }

    public RiskProfileWizard()
    {
        InitializeComponent();
        UpdateStepVisibility();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_step < TotalSteps)
        {
            _step++;
            UpdateStepVisibility();
        }
        else
        {
            // Finish
            Result = BuildProfile();
            DialogResult = true;
            Close();
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            UpdateStepVisibility();
        }
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        // Use defaults — Result stays null; caller will use RiskProfile.Default
        DialogResult = false;
        Close();
    }

    // ── Step visibility ───────────────────────────────────────────────────────

    private void UpdateStepVisibility()
    {
        Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Update dot colours
        SetDot(Dot1, _step >= 1);
        SetDot(Dot2, _step >= 2);
        SetDot(Dot3, _step >= 3);
        SetDot(Dot4, _step >= 4);

        BtnBack.Visibility = _step > 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Content    = _step == TotalSteps ? "Finish ✓" : "Next →";
    }

    private static void SetDot(System.Windows.Shapes.Ellipse dot, bool active)
        => dot.Fill = new SolidColorBrush(
               active ? Color.FromRgb(41, 98, 255)
                      : Color.FromRgb(44, 50, 70));

    // ── Build result profile from radio selections ────────────────────────────

    private RiskProfile BuildProfile()
    {
        double maxRisk = Risk05.IsChecked == true ? 0.5
                       : Risk2.IsChecked  == true ? 2.0
                       : 1.0;

        StopStyle stop = StopTight.IsChecked == true ? StopStyle.Tight
                       : StopWide.IsChecked  == true ? StopStyle.Wide
                       : StopStyle.Normal;

        int conc = Conc1.IsChecked == true ? 1
                 : Conc2.IsChecked == true ? 2
                 : Conc5.IsChecked == true ? 5
                 : 3;

        DrawdownTolerance dd = DdLow.IsChecked  == true ? DrawdownTolerance.Low
                             : DdHigh.IsChecked == true ? DrawdownTolerance.High
                             : DrawdownTolerance.Medium;

        return new RiskProfile
        {
            CreatedAt           = DateTimeOffset.UtcNow,
            MaxRiskPct          = maxRisk,
            StopStyle           = stop,
            MaxConcurrentTrades = conc,
            DrawdownTolerance   = dd,
            IsActive            = true,
        };
    }
}
