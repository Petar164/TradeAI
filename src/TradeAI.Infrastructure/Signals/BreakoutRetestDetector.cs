using TradeAI.Core;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Detects Breakout-Retest setups (Long only).
///
/// Algorithm:
///   1. Find a resistance zone: ≥2 swing highs within ATR×0.3 of each other
///      in the last 50 candles (before the recent breakout window).
///   2. Detect a breakout candle in the last 15 candles before the trigger:
///      strong bullish close above resistance (body ≥ 60% of range).
///   3. After the breakout, price retested (pulled back within ATR×0.3 of resistance).
///   4. Trigger candle closes back above resistance — retest confirmed as support.
///
/// Zone calculation:
///   Entry  : [resistance − ATR×0.1 .. resistance + ATR×0.3]
///   Stop   : retest_low − ATR×0.5
///   Target : entryMid + risk × 1.5
///   TTL    : 10 candles
/// </summary>
public sealed class BreakoutRetestDetector : ISignalDetector
{
    private const int    MinCandles           = 60;
    private const int    SwingLookback        = 50;
    private const int    BreakoutSearchWindow = 15;
    private const int    RetestSearchWindow   = 10;
    private const int    AtrPeriod            = 14;
    private const int    TtlCandles           = 10;
    private const double MinRR               = 1.5;

    public Task<Signal?> DetectAsync(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        try   { return Task.FromResult(Detect(candles, symbol, timeframe)); }
        catch { return Task.FromResult<Signal?>(null); }
    }

    private static Signal? Detect(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        if (candles.Count < MinCandles) return null;

        double atr = Indicators.Atr(candles, AtrPeriod);
        if (double.IsNaN(atr) || atr <= 0) return null;

        var trigger = candles[^1];
        int n       = candles.Count;

        // ── 1. Find resistance zone from swing highs ──────────────────────────
        int swingEnd   = n - RetestSearchWindow - BreakoutSearchWindow - 2;
        int swingStart = Math.Max(1, swingEnd - SwingLookback);
        if (swingEnd <= swingStart + 2) return null;

        var swingHighs = new List<double>();
        for (int i = swingStart + 1; i < swingEnd - 1; i++)
        {
            if (candles[i].High > candles[i - 1].High &&
                candles[i].High > candles[i + 1].High)
                swingHighs.Add(candles[i].High);
        }
        if (swingHighs.Count < 2) return null;

        swingHighs.Sort();
        double resistance = double.NaN;
        for (int i = 0; i < swingHighs.Count - 1; i++)
        {
            if (swingHighs[i + 1] - swingHighs[i] <= atr * 0.3)
            {
                resistance = (swingHighs[i] + swingHighs[i + 1]) / 2.0;
                break;
            }
        }
        if (double.IsNaN(resistance)) return null;

        // ── 2. Find breakout candle ───────────────────────────────────────────
        int bsEnd   = n - RetestSearchWindow - 2;
        int bsStart = Math.Max(1, bsEnd - BreakoutSearchWindow);

        int breakoutIdx = -1;
        for (int i = bsEnd; i >= bsStart; i--)
        {
            var c     = candles[i];
            double body  = c.Close - c.Open;
            double range = c.High - c.Low;

            if (c.Close > resistance + atr * 0.1 &&
                body  > 0 &&
                range > 0 &&
                body / range >= 0.6)
            {
                breakoutIdx = i;
                break;
            }
        }
        if (breakoutIdx < 0) return null;

        // ── 3. Retest: price pulls back near broken resistance ────────────────
        double retestLow = double.MaxValue;
        bool   retested  = false;
        for (int i = breakoutIdx + 1; i < n - 1; i++)
        {
            retestLow = Math.Min(retestLow, candles[i].Low);
            if (candles[i].Low <= resistance + atr * 0.3)
                retested = true;
        }
        if (!retested || retestLow == double.MaxValue) return null;

        // ── 4. Trigger closes above resistance ───────────────────────────────
        if (trigger.Close <= resistance) return null;

        // ── 5. Zone + R:R ─────────────────────────────────────────────────────
        double entryLow  = resistance - atr * 0.1;
        double entryHigh = resistance + atr * 0.3;
        double stopPrice = retestLow - atr * 0.5;
        double entryMid  = (entryLow + entryHigh) / 2.0;
        double risk      = entryMid - stopPrice;
        if (risk <= 0) return null;

        double targetLow  = entryMid + risk * MinRR;
        double targetHigh = targetLow + atr;
        if ((targetLow - entryMid) / risk < MinRR) return null;

        return new Signal
        {
            Symbol               = symbol,
            Timeframe            = timeframe,
            SignalType           = "BreakoutRetest",
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
