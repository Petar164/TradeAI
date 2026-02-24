using TradeAI.Core;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Extracts a 20-element normalised feature vector from a candle window.
///
/// All 20 features are scaled to [0, 1] using domain-knowledge bounds so that
/// Euclidean kNN distances are comparable across features.
///
/// Feature index reference:
///  0  RSI(14) / 100
///  1  (Close − EMA20) / ATR  — clamp [−3, 3]
///  2  (Close − EMA50) / ATR  — clamp [−3, 3]
///  3  (EMA20 − EMA50) / ATR  — clamp [−3, 3]
///  4  Bollinger Band position: (Close − Lower) / (Upper − Lower)
///  5  BB width: (Upper − Lower) / Middle, clamp [0, 0.10]
///  6  ATR / Close (volatility %) — clamp [0, 0.05]
///  7  Trigger body ratio: |Close − Open| / Range
///  8  Trigger upper wick: (High − max(C,O)) / Range
///  9  Trigger lower wick: (min(C,O) − Low) / Range
/// 10  Trigger close position: (Close − Low) / Range
/// 11  Trigger is bullish: Close > Open ? 1 : 0
/// 12  Volume vs 20-bar average — clamp [0, 5×]
/// 13  10-bar price momentum: (Close − Close[−10]) / ATR — clamp [−5, 5]
/// 14   5-bar price momentum: (Close − Close[−5])  / ATR — clamp [−5, 5]
/// 15  Dist. to 20-bar swing high: (SwingHigh − Close) / ATR — clamp [0, 5]
/// 16  Dist. to 20-bar swing low:  (Close − SwingLow)  / ATR — clamp [0, 5]
/// 17  Consecutive bullish candles in last 10 / 10
/// 18  (Close − VWAP) / ATR — clamp [−3, 3]
/// 19  Trigger range / ATR — clamp [0, 5]
/// </summary>
public sealed class FeatureVectorBuilder
{
    private const int AtrPeriod = 14;
    private const int RsiPeriod = 14;
    private const int BbPeriod  = 20;

    public float[] Build(IReadOnlyList<Candle> candles)
    {
        const int Len = 20;
        if (candles.Count < 60) return new float[Len];

        var    trigger = candles[^1];
        int    n       = candles.Count;
        double close   = trigger.Close;
        double range   = trigger.High - trigger.Low;
        if (range < 1e-10) range = 1e-10;

        // ── Indicators ─────────────────────────────────────────────────────────
        double atr = Indicators.Atr(candles, AtrPeriod);
        if (double.IsNaN(atr) || atr <= 0) atr = 1e-10;

        double rsi = Indicators.Rsi(candles, RsiPeriod);
        if (double.IsNaN(rsi)) rsi = 50;

        double ema20 = Indicators.Ema(candles, 20);
        double ema50 = Indicators.Ema(candles, 50);
        if (double.IsNaN(ema20)) ema20 = close;
        if (double.IsNaN(ema50)) ema50 = close;

        var (bbMid, bbUpper, bbLower) = Indicators.BollingerBands(candles, BbPeriod, 2.0);
        if (double.IsNaN(bbMid) || bbUpper <= bbLower)
        { bbMid = close; bbUpper = close + atr; bbLower = close - atr; }

        double vwap = Indicators.Vwap(candles);
        if (double.IsNaN(vwap)) vwap = close;

        // ── Volume 20-bar average ───────────────────────────────────────────────
        int    volCount = Math.Min(20, n - 1);
        double avgVol   = 0;
        for (int i = n - 1 - volCount; i < n - 1; i++) avgVol += candles[i].Volume;
        if (volCount > 0) avgVol /= volCount;
        if (avgVol <= 0) avgVol = 1;

        // ── Swing high/low over last 20 bars (excluding trigger) ───────────────
        double swingHigh = double.MinValue, swingLow = double.MaxValue;
        for (int i = Math.Max(0, n - 21); i < n - 1; i++)
        {
            if (candles[i].High > swingHigh) swingHigh = candles[i].High;
            if (candles[i].Low  < swingLow)  swingLow  = candles[i].Low;
        }
        if (swingHigh == double.MinValue) swingHigh = close;
        if (swingLow  == double.MaxValue) swingLow  = close;

        // ── Consecutive bullish candles (last 10, not including trigger) ────────
        int consecutiveBull = 0;
        for (int i = n - 2; i >= Math.Max(0, n - 11); i--)
        {
            if (candles[i].Close > candles[i].Open) consecutiveBull++;
            else break;
        }

        // ── Build feature array ─────────────────────────────────────────────────
        var f = new float[Len];

        f[0]  = Norm01(rsi / 100.0);
        f[1]  = NormSymmetric((close - ema20)  / atr, 3);
        f[2]  = NormSymmetric((close - ema50)  / atr, 3);
        f[3]  = NormSymmetric((ema20 - ema50)  / atr, 3);
        f[4]  = Norm01(bbUpper > bbLower
                    ? (close - bbLower) / (bbUpper - bbLower)
                    : 0.5);
        f[5]  = Norm01(Math.Min((bbUpper - bbLower) / (bbMid > 0 ? bbMid : 1), 0.10) / 0.10);
        f[6]  = Norm01(Math.Min(atr / (close > 0 ? close : 1), 0.05) / 0.05);
        f[7]  = Norm01(Math.Abs(trigger.Close - trigger.Open) / range);
        f[8]  = Norm01((trigger.High - Math.Max(trigger.Close, trigger.Open)) / range);
        f[9]  = Norm01((Math.Min(trigger.Close, trigger.Open) - trigger.Low) / range);
        f[10] = Norm01((close - trigger.Low) / range);
        f[11] = trigger.Close > trigger.Open ? 1f : 0f;
        f[12] = Norm01(Math.Min(trigger.Volume / avgVol, 5.0) / 5.0);
        f[13] = n > 10 ? NormSymmetric((close - candles[n - 11].Close) / atr, 5) : 0.5f;
        f[14] = n > 5  ? NormSymmetric((close - candles[n -  6].Close) / atr, 5) : 0.5f;
        f[15] = Norm01(Math.Min((swingHigh - close) / atr, 5.0) / 5.0);
        f[16] = Norm01(Math.Min((close - swingLow)  / atr, 5.0) / 5.0);
        f[17] = Norm01(consecutiveBull / 10.0);
        f[18] = NormSymmetric((close - vwap) / atr, 3);
        f[19] = Norm01(Math.Min(range / atr, 5.0) / 5.0);

        return f;
    }

    // ── Normalisation helpers ──────────────────────────────────────────────────

    /// <summary>Clamps a [0,1] value to [0,1] (no-op, but ensures NaN safety).</summary>
    private static float Norm01(double v) => (float)Math.Clamp(v, 0.0, 1.0);

    /// <summary>Maps a symmetric range [−bound, +bound] to [0, 1].</summary>
    private static float NormSymmetric(double v, double bound)
        => (float)((Math.Clamp(v, -bound, bound) + bound) / (2.0 * bound));
}
