using TradeAI.Core;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Detects Trend Continuation setups on a closed candle.
///
/// Algorithm (Long only — mirror for Short in a later sprint):
///   1. EMA20 &gt; EMA50  →  uptrend confirmed
///   2. In the last 2–8 candles before the trigger, at least one candle's
///      low touched the EMA20 zone (within ATR×0.5) or dipped below it.
///   3. The most recent (trigger) candle closes ABOVE EMA20 — recovery confirmed.
///   4. A prior swing high exists above EMA20 in the 30 candles before the pullback.
///   5. The setup risk:reward is at least 1.5 : 1.
///
/// Zone calculation:
///   Entry  : [pullback_low .. EMA20]
///   Stop   : pullback_low − ATR × 0.5
///   Target : [swing_high .. swing_high + ATR]
///   TTL    : 8 candles
/// </summary>
public sealed class TrendContinuationDetector : ISignalDetector
{
    private const int MinCandles        = 60;
    private const int EmaFast           = 20;
    private const int EmaSlow           = 50;
    private const int AtrPeriod         = 14;
    private const int PullbackWindow    = 8;   // candles before trigger to scan for pullback
    private const int SwingHighLookback = 30;  // candles before pullback window for swing high
    private const int TtlCandles        = 8;
    private const double MinRR          = 1.5;

    public Task<Signal?> DetectAsync(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        try   { return Task.FromResult(Detect(candles, symbol, timeframe)); }
        catch { return Task.FromResult<Signal?>(null); }
    }

    private static Signal? Detect(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        if (candles.Count < MinCandles) return null;

        double ema20 = Indicators.Ema(candles, EmaFast);
        double ema50 = Indicators.Ema(candles, EmaSlow);
        double atr   = Indicators.Atr(candles, AtrPeriod);

        if (double.IsNaN(ema20) || double.IsNaN(ema50) || double.IsNaN(atr) || atr <= 0)
            return null;

        // ── 1. Uptrend ────────────────────────────────────────────────────────
        if (ema20 <= ema50) return null;

        // ── 2. Trigger candle closes above EMA20 ─────────────────────────────
        var trigger = candles[^1];
        if (trigger.Close <= ema20) return null;

        // ── 3. Pullback window: scan the 2–PullbackWindow candles before trigger
        int pbEnd   = candles.Count - 2;              // last non-trigger candle
        int pbStart = Math.Max(1, pbEnd - PullbackWindow + 1);

        bool touchedEma  = false;
        double pullbackLow = double.MaxValue;

        for (int i = pbStart; i <= pbEnd; i++)
        {
            var c = candles[i];
            pullbackLow = Math.Min(pullbackLow, c.Low);

            // "Touched" means the wick came within half an ATR of EMA20 or dipped below
            if (c.Low <= ema20 + atr * 0.5)
                touchedEma = true;
        }

        if (!touchedEma || pullbackLow == double.MaxValue) return null;

        // ── 4. Swing high: highest high in 30 candles before the pullback window
        int swingEnd   = pbStart - 1;
        int swingStart = Math.Max(0, swingEnd - SwingHighLookback);

        double swingHigh = double.MinValue;
        for (int i = swingStart; i <= swingEnd; i++)
            swingHigh = Math.Max(swingHigh, candles[i].High);

        // Need a meaningful swing above EMA20
        if (swingHigh <= ema20 || swingHigh - ema20 < atr * 0.5) return null;

        // Pullback must represent a real retracement (not just noise)
        if (swingHigh - pullbackLow < atr) return null;

        // ── 5. Zone + R:R validation ──────────────────────────────────────────
        double entryLow   = pullbackLow;
        double entryHigh  = ema20;
        double stopPrice  = pullbackLow - atr * 0.5;
        double targetLow  = swingHigh;
        double targetHigh = swingHigh + atr;

        double risk   = (entryLow + entryHigh) / 2.0 - stopPrice;
        double reward = targetLow - (entryLow + entryHigh) / 2.0;

        if (risk <= 0 || reward / risk < MinRR) return null;

        return new Signal
        {
            Symbol               = symbol,
            Timeframe            = timeframe,
            SignalType           = "TrendContinuation",
            Direction            = TradeDirection.Long,
            DetectedAtCandleTime = trigger.OpenTime,
            EntryLow             = entryLow,
            EntryHigh            = entryHigh,
            StopPrice            = stopPrice,
            TargetLow            = targetLow,
            TargetHigh           = targetHigh,
            TtlCandles           = TtlCandles,
            State                = OverlayState.Pending,
        };
    }
}
