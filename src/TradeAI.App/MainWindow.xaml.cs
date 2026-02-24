using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Core.Models;
using TradeAI.Infrastructure.MarketData;
using TradeAI.Infrastructure.Settings;
using TradeAI.UI.ViewModels;

namespace TradeAI.App;

public partial class MainWindow : Window
{
    private readonly ChartViewModel      _chartVm;
    private readonly DataFeedManager     _feedManager;
    private readonly AppSettings         _settings;
    private readonly IRiskProfileService _riskService;

    // Track signal cards by ID so we can update them on state change
    private readonly Dictionary<int, Border> _signalCards = new();
    private bool _emptyStateVisible = true;

    // Strong references so SignalBus WeakReferences stay alive
    private readonly Action<SignalDetectedEvent>        _onSignalDetected;
    private readonly Action<OverlayStateChangedEvent>   _onOverlayStateChanged;

#pragma warning disable IDE0052
    private readonly IDisposable _signalSub;
    private readonly IDisposable _overlaySub;
#pragma warning restore IDE0052

    public MainWindow(
        ChartViewModel       chartVm,
        DataFeedManager      feedManager,
        AppSettings          settings,
        SignalBus            bus,
        IRiskProfileService  riskService)
    {
        _chartVm     = chartVm;
        _feedManager = feedManager;
        _settings    = settings;
        _riskService = riskService;

        _onSignalDetected      = OnSignalDetected;
        _onOverlayStateChanged = OnOverlayStateChanged;
        _signalSub  = bus.Subscribe<SignalDetectedEvent>(_onSignalDetected);
        _overlaySub = bus.Subscribe<OverlayStateChangedEvent>(_onOverlayStateChanged);

        InitializeComponent();
        ChartPanel.DataContext = _chartVm;
        SizeToWorkArea();
        SetActiveTimeframe(_settings.ActiveTimeframe);
    }

    // ── Signal panel ───────────────────────────────────────────────────────────

    private void OnSignalDetected(SignalDetectedEvent e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Remove hardcoded empty-state content on first signal
            if (_emptyStateVisible)
            {
                SignalCards.Children.Clear();
                _emptyStateVisible = false;
            }

            var card = BuildSignalCard(e.Signal);
            _signalCards[e.Signal.Id] = card;
            SignalCards.Children.Insert(0, card);

            // Cap at 10 visible cards
            while (SignalCards.Children.Count > 10)
                SignalCards.Children.RemoveAt(SignalCards.Children.Count - 1);
        });
    }

    private void OnOverlayStateChanged(OverlayStateChangedEvent e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_signalCards.TryGetValue(e.SignalId, out var card)) return;

            // Dim the card on terminal states
            if (e.NewState is OverlayState.TargetHit)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 193, 118));
                card.Opacity = 0.65;
            }
            else if (e.NewState is OverlayState.StopHit)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 59, 59));
                card.Opacity = 0.65;
            }
            else if (e.NewState is OverlayState.Expired)
            {
                card.Opacity = 0.35;
            }

            if (e.NewState is OverlayState.TargetHit or OverlayState.StopHit or OverlayState.Expired)
                _signalCards.Remove(e.SignalId);
        });
    }

    // ── Card builder ──────────────────────────────────────────────────────────

    private static Border BuildSignalCard(Signal signal)
    {
        // Type colour (matches chart arrows)
        var typeColor = signal.SignalType switch
        {
            "TrendContinuation" => Color.FromRgb(41,  98,  255),
            "BreakoutRetest"    => Color.FromRgb(255, 109, 0),
            "MeanReversion"     => Color.FromRgb(170, 0,   255),
            "SRBounce"          => Color.FromRgb(255, 214, 0),
            _                   => Color.FromRgb(41,  98,  255),
        };
        var typeBrush = new SolidColorBrush(typeColor);

        var isLong       = signal.Direction == TradeDirection.Long;
        var dirColor     = isLong
            ? Color.FromRgb(0, 193, 118)
            : Color.FromRgb(255, 59, 59);
        var dirText      = isLong ? "LONG ▲" : "SHORT ▼";

        // ── Card outer border ──────────────────────────────────────────────────
        var card = new Border
        {
            Margin          = new Thickness(12, 6, 12, 0),
            Padding         = new Thickness(12),
            CornerRadius    = new CornerRadius(6),
            Background      = new SolidColorBrush(Color.FromRgb(22, 26, 38)),
            BorderThickness = new Thickness(1),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(44, 50, 70)),
        };

        var stack = new StackPanel();
        card.Child = stack;

        // ── Row 1: Type chip + direction ──────────────────────────────────────
        var row1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var typeChip = new Border
        {
            CornerRadius = new CornerRadius(3),
            Background   = new SolidColorBrush(Color.FromArgb(40, typeColor.R, typeColor.G, typeColor.B)),
            BorderBrush  = typeBrush,
            BorderThickness = new Thickness(1),
            Padding      = new Thickness(6, 2, 6, 2),
        };
        typeChip.Child = new TextBlock
        {
            Text       = signal.SignalType switch
            {
                "TrendContinuation" => "Trend Continuation",
                "BreakoutRetest"    => "Breakout Retest",
                "MeanReversion"     => "Mean Reversion",
                "SRBounce"          => "S/R Bounce",
                _                   => signal.SignalType,
            },
            FontSize   = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = typeBrush,
        };
        Grid.SetColumn(typeChip, 0);

        var dirLabel = new TextBlock
        {
            Text       = dirText,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(dirColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(dirLabel, 2);

        row1.Children.Add(typeChip);
        row1.Children.Add(dirLabel);
        stack.Children.Add(row1);

        // ── Row 2: Large probability badge ───────────────────────────────────
        var probRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        if (signal.ConfidencePct.HasValue)
        {
            var probText = new TextBlock
            {
                Text       = $"{signal.ConfidencePct:F0}%",
                FontSize   = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            var probMeta = new TextBlock
            {
                Text       = $" win prob  ·  {signal.SimilaritySampleCount ?? 0}/10 neighbours",
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 140)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin     = new Thickness(0, 0, 0, 4),
            };
            probRow.Children.Add(probText);
            probRow.Children.Add(probMeta);
            stack.Children.Add(probRow);

            // ── Row 3: Neighbour outcome squares (approximate from probability) ──
            var squareRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            int hits  = (int)Math.Round((signal.ConfidencePct.Value / 100.0) * (signal.SimilaritySampleCount ?? 10));
            int total = signal.SimilaritySampleCount ?? 10;
            for (int i = 0; i < total; i++)
            {
                var sq = new Border
                {
                    Width           = 14,
                    Height          = 9,
                    CornerRadius    = new CornerRadius(2),
                    Margin          = new Thickness(0, 0, 2, 0),
                    Background      = i < hits
                        ? new SolidColorBrush(Color.FromRgb(0, 193, 118))
                        : new SolidColorBrush(Color.FromRgb(255, 59, 59)),
                };
                squareRow.Children.Add(sq);
            }
            stack.Children.Add(squareRow);
        }
        else
        {
            var noProb = new TextBlock
            {
                Text       = "— awaiting data",
                FontSize   = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 140)),
                Margin     = new Thickness(0, 0, 0, 8),
            };
            stack.Children.Add(noProb);
        }

        // ── Row 4: Separator ─────────────────────────────────────────────────
        stack.Children.Add(new Border
        {
            Height          = 1,
            Background      = new SolidColorBrush(Color.FromRgb(44, 50, 70)),
            Margin          = new Thickness(0, 0, 0, 8),
        });

        // ── Row 5: Price levels grid ──────────────────────────────────────────
        var priceGrid = new Grid();
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition());
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition());
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition());

        priceGrid.Children.Add(MakePriceLabel("Entry",
            $"{signal.EntryLow:F2}–{signal.EntryHigh:F2}",
            Color.FromRgb(41, 98, 255), 0));
        priceGrid.Children.Add(MakePriceLabel("Stop",
            $"{signal.StopPrice:F2}",
            Color.FromRgb(255, 59, 59), 1));
        priceGrid.Children.Add(MakePriceLabel("Target",
            $"{signal.TargetLow:F2}–{signal.TargetHigh:F2}",
            Color.FromRgb(0, 193, 118), 2));

        stack.Children.Add(priceGrid);

        // ── Row 6: R:R ratio ──────────────────────────────────────────────────
        stack.Children.Add(new TextBlock
        {
            Text       = $"R:R  {signal.RRatio:F2}  ·  TTL {signal.TtlCandles} candles",
            FontSize   = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 140)),
            Margin     = new Thickness(0, 8, 0, 0),
        });

        return card;
    }

    private static UIElement MakePriceLabel(string label, string value, Color color, int col)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text       = label,
            FontSize   = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 140)),
        });
        sp.Children.Add(new TextBlock
        {
            Text       = value,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color),
        });
        Grid.SetColumn(sp, col);
        return sp;
    }

    // ── Chat override bar ─────────────────────────────────────────────────────

    private void ChatSendButton_Click(object sender, RoutedEventArgs e)
        => SendChatCommand();

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendChatCommand();
            e.Handled = true;
        }
    }

    private void SendChatCommand()
    {
        var text = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var response = _riskService.ApplyOverride(text);

        ChatInput.Clear();
        ChatResponseLabel.Text       = response ?? "Command not recognised. Try: \"too risky\", \"widen stops\", \"reset\".";
        ChatResponseLabel.Foreground = response is not null
            ? (System.Windows.Media.Brush)FindResource("BrushAccent")
            : new SolidColorBrush(Color.FromRgb(255, 59, 59));
        ChatResponseLabel.Visibility = Visibility.Visible;
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
