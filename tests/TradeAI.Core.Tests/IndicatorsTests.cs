using TradeAI.Core;
using TradeAI.Core.Models;
using Xunit;

namespace TradeAI.Core.Tests;

/// <summary>
/// Unit tests for <see cref="Indicators"/>.
///
/// Candle helper: MakeCandles(closes) builds minimal candles with
/// Open == Close - 1, High == Close + 1, Low == Close - 1, Volume == 1_000.
/// </summary>
public class IndicatorsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Candle C(double close, double? high = null, double? low = null, double volume = 1_000)
        => new(0, "TEST", "5m",
               DateTimeOffset.UtcNow,
               Open:   close - 1,
               High:   high  ?? close + 1,
               Low:    low   ?? close - 1,
               Close:  close,
               Volume: volume);

    private static List<Candle> Candles(params double[] closes)
        => closes.Select(c => C(c)).ToList();

    private const double Tol = 1e-9;

    // ── EMA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Ema_InsufficientData_ReturnsNaN()
    {
        var candles = Candles(1, 2, 3);            // 3 candles, period 5
        Assert.True(double.IsNaN(Indicators.Ema(candles, 5)));
    }

    [Fact]
    public void Ema_ExactlyOnePeriod_ReturnsSma()
    {
        var candles = Candles(10, 20, 30);
        double ema = Indicators.Ema(candles, 3);
        Assert.Equal(20.0, ema, precision: 10);    // SMA(10,20,30) = 20
    }

    [Fact]
    public void Ema_SingleCandle_PeriodOne_ReturnsClose()
    {
        var candles = Candles(42);
        Assert.Equal(42.0, Indicators.Ema(candles, 1), precision: 10);
    }

    [Fact]
    public void Ema_Trending_EmaHigherThanSmaLag()
    {
        // Rising closes: EMA should be closer to recent values than plain SMA
        var candles = Candles(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        double ema = Indicators.Ema(candles, 3);
        Assert.True(ema > 8.5, $"EMA expected > 8.5, got {ema}");
    }

    [Fact]
    public void Ema_ZeroPeriod_ReturnsNaN()
    {
        var candles = Candles(1, 2, 3);
        Assert.True(double.IsNaN(Indicators.Ema(candles, 0)));
    }

    // ── ATR ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Atr_InsufficientData_ReturnsNaN()
    {
        // Need period + 1 candles; here period=5, only 5 candles
        var candles = Candles(1, 2, 3, 4, 5);
        Assert.True(double.IsNaN(Indicators.Atr(candles, 5)));
    }

    [Fact]
    public void Atr_ConstantCandles_ReturnsRange()
    {
        // All candles have High=close+1, Low=close-1, same close → TR = 2 every bar
        var candles = Enumerable.Repeat(C(100, high: 101, low: 99), 15).ToList();
        double atr = Indicators.Atr(candles, 14);
        Assert.Equal(2.0, atr, precision: 10);
    }

    [Fact]
    public void Atr_IsPositive()
    {
        var candles = Candles(10, 12, 9, 11, 13, 8, 14, 10, 11, 12, 9, 10, 11, 13, 12);
        double atr = Indicators.Atr(candles, 14);
        Assert.True(atr > 0, $"ATR should be positive, got {atr}");
    }

    [Fact]
    public void Atr_LargeSpikeRaisesAtr()
    {
        // Smooth candles, then one large spike
        var candles = new List<Candle>();
        for (int i = 0; i < 14; i++) candles.Add(C(100, high: 101, low: 99));
        candles.Add(C(100, high: 120, low: 80));   // huge spike at end

        double atrNoSpike = Indicators.Atr(candles.Take(14 + 1 - 1).Append(C(100, high: 101, low: 99)).ToList(), 14);
        double atrSpike   = Indicators.Atr(candles, 14);
        Assert.True(atrSpike > atrNoSpike, "Spike candle should raise ATR");
    }

    // ── RSI ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Rsi_InsufficientData_ReturnsNaN()
    {
        var candles = Candles(1, 2, 3);           // need period + 1 = 15
        Assert.True(double.IsNaN(Indicators.Rsi(candles, 14)));
    }

    [Fact]
    public void Rsi_AllUpMoves_Returns100()
    {
        // Monotonically rising closes → average loss = 0 → RSI = 100
        var candles = Enumerable.Range(1, 20).Select(i => C(i * 1.0)).ToList();
        Assert.Equal(100.0, Indicators.Rsi(candles, 14), precision: 10);
    }

    [Fact]
    public void Rsi_AllDownMoves_ReturnsNearZero()
    {
        // Monotonically falling closes → RSI approaches 0
        var candles = Enumerable.Range(0, 20).Select(i => C(20.0 - i)).ToList();
        double rsi = Indicators.Rsi(candles, 14);
        Assert.True(rsi < 5, $"All-down RSI expected < 5, got {rsi}");
    }

    [Fact]
    public void Rsi_NeutralSeries_ReturnsNear50()
    {
        // Alternating up/down of equal size → RS ≈ 1 → RSI ≈ 50
        var closes = new double[20];
        for (int i = 0; i < 20; i++) closes[i] = 100 + (i % 2 == 0 ? 1 : -1);
        double rsi = Indicators.Rsi(Candles(closes), 14);
        Assert.True(rsi is > 40 and < 60, $"Neutral RSI expected ≈50, got {rsi}");
    }

    [Fact]
    public void Rsi_InRange()
    {
        var closes = new double[] { 44, 44.34, 44.09, 44.15, 43.61, 44.33, 44.83, 45.10,
                                    45.15, 43.61, 44.33, 44.83, 45.10, 45.15, 43.61 };
        double rsi = Indicators.Rsi(Candles(closes), 14);
        Assert.True(rsi is >= 0 and <= 100, $"RSI must be 0–100, got {rsi}");
    }

    // ── Bollinger Bands ───────────────────────────────────────────────────────

    [Fact]
    public void BollingerBands_InsufficientData_ReturnsNaN()
    {
        var candles = Candles(1, 2, 3);
        var (m, u, l) = Indicators.BollingerBands(candles, 5);
        Assert.True(double.IsNaN(m) && double.IsNaN(u) && double.IsNaN(l));
    }

    [Fact]
    public void BollingerBands_ConstantPrice_BandsAreMid()
    {
        // All closes equal → std dev = 0 → upper == middle == lower
        var candles = Enumerable.Repeat(C(50), 20).ToList();
        var (m, u, l) = Indicators.BollingerBands(candles, 20);
        Assert.Equal(50.0, m, precision: 10);
        Assert.Equal(50.0, u, precision: 10);
        Assert.Equal(50.0, l, precision: 10);
    }

    [Fact]
    public void BollingerBands_UpperAboveLower()
    {
        var candles = Candles(10, 12, 9, 14, 11, 13, 8, 15, 10, 11, 12, 9, 14, 11, 13, 8, 15, 10, 11, 12);
        var (m, u, l) = Indicators.BollingerBands(candles, 20);
        Assert.True(u > m && m > l, $"Expected upper > middle > lower, got {u}, {m}, {l}");
    }

    [Fact]
    public void BollingerBands_WiderK_WiderBands()
    {
        var candles = Candles(10, 12, 9, 14, 11, 13, 8, 15, 10, 11, 12, 9, 14, 11, 13, 8, 15, 10, 11, 12);
        var (_, u2, l2) = Indicators.BollingerBands(candles, 20, k: 2);
        var (_, u3, l3) = Indicators.BollingerBands(candles, 20, k: 3);
        Assert.True(u3 > u2 && l3 < l2, "k=3 bands should be wider than k=2");
    }

    // ── VWAP ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Vwap_EmptyList_ReturnsNaN()
    {
        Assert.True(double.IsNaN(Indicators.Vwap(new List<Candle>())));
    }

    [Fact]
    public void Vwap_ZeroVolume_ReturnsNaN()
    {
        var candles = new List<Candle> { C(100, volume: 0), C(200, volume: 0) };
        Assert.True(double.IsNaN(Indicators.Vwap(candles)));
    }

    [Fact]
    public void Vwap_EqualVolumes_EqualsMeanTypicalPrice()
    {
        // Equal volumes → VWAP = mean of typical prices
        var candles = new List<Candle>
        {
            C(10, high: 12, low: 8,  volume: 500),   // TP = (12+8+10)/3  ≈ 10
            C(20, high: 22, low: 18, volume: 500),   // TP = (22+18+20)/3 ≈ 20
        };
        double vwap = Indicators.Vwap(candles);
        double expectedTp1 = (12 + 8  + 10) / 3.0;
        double expectedTp2 = (22 + 18 + 20) / 3.0;
        double expected    = (expectedTp1 + expectedTp2) / 2.0;
        Assert.Equal(expected, vwap, Tol);
    }

    [Fact]
    public void Vwap_WeightedToHighVolumeBar()
    {
        // One bar with much higher volume should pull VWAP toward its price
        var candles = new List<Candle>
        {
            C(10, high: 11, low: 9,    volume: 100),
            C(100, high: 101, low: 99, volume: 10_000),
        };
        double vwap = Indicators.Vwap(candles);
        Assert.True(vwap > 90, $"VWAP expected > 90 due to high-volume bar at 100, got {vwap}");
    }

    [Fact]
    public void Vwap_SingleCandle_EqualsTypicalPrice()
    {
        var c = C(30, high: 33, low: 27, volume: 1_000);
        double expected = (33 + 27 + 30) / 3.0;
        Assert.Equal(expected, Indicators.Vwap(new List<Candle> { c }), Tol);
    }
}
