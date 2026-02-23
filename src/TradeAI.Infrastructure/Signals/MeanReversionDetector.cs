using TradeAI.Core;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Detects Mean Reversion setups (both Long and Short).
///
/// Algorithm:
///   SHORT setup:
///     1. Previous candle's close pushed above Bollinger upper band (BB 20,2).
///     2. RSI(14) > 72 — overbought.
///     3. Trigger candle is an exhaustion candle: small body (< 35% of range),
///        large upper wick (> 40% of range), and close comes back inside the band.
///   LONG setup (mirror):
///     1. Previous candle's close pushed below Bollinger lower band.
///     2. RSI(14) < 28 — oversold.
///     3. Trigger candle is a hammer: small body, large lower wick, close inside band.
///
/// Zone calculation:
///   Entry  : [trigger.Close ± ATR×0.1]
///   Stop   : trigger.High + ATR×0.5  (Short) | trigger.Low − ATR×0.5  (Long)
///   Target : middle band ± ATR×0.5
///   TTL    : 6 candles
/// </summary>
public sealed class MeanReversionDetector : ISignalDetector
{
    private const int    MinCandles      = 30;
    private const int    BbPeriod        = 20;
    private const double BbStdDev        = 2.0;
    private const int    RsiPeriod       = 14;
    private const int    AtrPeriod       = 14;
    private const int    TtlCandles      = 6;
    private const double MinRR          = 1.5;
    private const double OverboughtRsi  = 72.0;
    private const double OversoldRsi    = 28.0;

    public Task<Signal?> DetectAsync(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        try   { return Task.FromResult(Detect(candles, symbol, timeframe)); }
        catch { return Task.FromResult<Signal?>(null); }
    }

    private static Signal? Detect(IReadOnlyList<Candle> candles, string symbol, string timeframe)
    {
        if (candles.Count < MinCandles) return null;

        var (middle, upper, lower) = Indicators.BollingerBands(candles, BbPeriod, BbStdDev);
        double rsi = Indicators.Rsi(candles, RsiPeriod);
        double atr = Indicators.Atr(candles, AtrPeriod);

        if (double.IsNaN(middle) || double.IsNaN(upper) || double.IsNaN(lower) ||
            double.IsNaN(rsi)    || double.IsNaN(atr)   || atr <= 0)
            return null;

        var trigger = candles[^1];
        var prev    = candles[^2];

        // ── SHORT: exhaustion at upper band ───────────────────────────────────
        if (prev.Close > upper && rsi > OverboughtRsi)
        {
            double body      = Math.Abs(trigger.Close - trigger.Open);
            double range     = trigger.High - trigger.Low;
            double upperWick = trigger.High - Math.Max(trigger.Close, trigger.Open);

            bool isExhaustion = range > 0            &&
                                body / range < 0.35  &&   // small body
                                upperWick / range > 0.40 &&   // large upper wick
                                trigger.Close < upper;         // closed back inside band

            if (!isExhaustion) return null;

            double entryLow  = trigger.Close - atr * 0.1;
            double entryHigh = trigger.Close + atr * 0.1;
            double stopPrice = trigger.High + atr * 0.5;
            double entryMid  = (entryLow + entryHigh) / 2.0;
            double risk      = stopPrice - entryMid;
            if (risk <= 0) return null;

            double targetHigh = middle;
            double targetLow  = middle - atr * 0.5;
            double reward     = entryMid - targetHigh;
            if (reward / risk < MinRR) return null;

            return new Signal
            {
                Symbol               = symbol,
                Timeframe            = timeframe,
                SignalType           = "MeanReversion",
                Direction            = TradeDirection.Short,
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

        // ── LONG: hammer at lower band ────────────────────────────────────────
        if (prev.Close < lower && rsi < OversoldRsi)
        {
            double body      = Math.Abs(trigger.Close - trigger.Open);
            double range     = trigger.High - trigger.Low;
            double lowerWick = Math.Min(trigger.Close, trigger.Open) - trigger.Low;

            bool isHammer = range > 0             &&
                            body / range < 0.35   &&   // small body
                            lowerWick / range > 0.40 &&   // large lower wick
                            trigger.Close > lower;         // closed back inside band

            if (!isHammer) return null;

            double entryLow  = trigger.Close - atr * 0.1;
            double entryHigh = trigger.Close + atr * 0.1;
            double stopPrice = trigger.Low - atr * 0.5;
            double entryMid  = (entryLow + entryHigh) / 2.0;
            double risk      = entryMid - stopPrice;
            if (risk <= 0) return null;

            double targetLow  = middle;
            double targetHigh = middle + atr * 0.5;
            double reward     = targetLow - entryMid;
            if (reward / risk < MinRR) return null;

            return new Signal
            {
                Symbol               = symbol,
                Timeframe            = timeframe,
                SignalType           = "MeanReversion",
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

        return null;
    }
}
