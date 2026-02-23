using TradeAI.Core;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// Detects Support/Resistance Bounce setups (both Long and Short).
///
/// Algorithm:
///   1. Build S/R zones from swing highs/lows in the last 100 candles.
///      A zone requires ≥2 swing points clustered within ATR×0.3 of each other.
///   2. Check if trigger candle is near a zone (within ATR×0.5).
///   3. Detect bounce:
///      LONG (support bounce): lower wick > 35% of range AND close above zone.
///      SHORT (resistance rejection): upper wick > 35% of range AND close below zone.
///   4. Volume spike: trigger volume > 1.3× 10-candle average.
///
/// Zone calculation:
///   Entry  : zone level ± ATR×0.2
///   Stop   : trigger extreme + ATR×0.5
///   Target : next opposing S/R zone, or entryMid ± risk×1.5
///   TTL    : 10 candles
/// </summary>
public sealed class SupportResistanceBounceDetector : ISignalDetector
{
    private const int    MinCandles   = 60;
    private const int    ZoneLookback = 100;
    private const int    AtrPeriod    = 14;
    private const int    TtlCandles   = 10;
    private const double MinRR       = 1.5;

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

        int zoneStart = Math.Max(1, n - ZoneLookback - 1);
        int zoneEnd   = n - 2;   // exclude trigger candle

        var swingHighs = new List<double>();
        var swingLows  = new List<double>();

        for (int i = zoneStart + 1; i < zoneEnd - 1; i++)
        {
            if (candles[i].High > candles[i - 1].High && candles[i].High > candles[i + 1].High)
                swingHighs.Add(candles[i].High);
            if (candles[i].Low  < candles[i - 1].Low  && candles[i].Low  < candles[i + 1].Low)
                swingLows.Add(candles[i].Low);
        }

        double avgVol = AverageVolume(candles, n - 11, n - 2);
        bool   volSpike = avgVol > 0 && trigger.Volume > avgVol * 1.3;
        if (!volSpike) return null;

        // ── LONG: support bounce ──────────────────────────────────────────────
        double? support = FindZone(swingLows, atr);
        if (support.HasValue)
        {
            double s = support.Value;
            bool near = trigger.Low  <= s + atr * 0.5 &&
                        trigger.Low  >= s - atr * 0.5;
            if (near)
            {
                double lowerWick = Math.Min(trigger.Close, trigger.Open) - trigger.Low;
                double range     = trigger.High - trigger.Low;
                bool   bounce    = range > 0 && lowerWick / range > 0.35 && trigger.Close > s;

                if (bounce)
                {
                    double entryLow  = s - atr * 0.1;
                    double entryHigh = s + atr * 0.3;
                    double stopPrice = trigger.Low - atr * 0.5;
                    double entryMid  = (entryLow + entryHigh) / 2.0;
                    double risk      = entryMid - stopPrice;
                    if (risk > 0)
                    {
                        double? res       = FindZone(swingHighs, atr);
                        double  targetLow = res.HasValue && res.Value > trigger.Close
                            ? res.Value - atr * 0.2
                            : entryMid + risk * MinRR;
                        double  targetHigh = targetLow + atr;
                        double  reward     = targetLow - entryMid;

                        if (reward / risk >= MinRR)
                        {
                            return new Signal
                            {
                                Symbol               = symbol,
                                Timeframe            = timeframe,
                                SignalType           = "SRBounce",
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
                }
            }
        }

        // ── SHORT: resistance rejection ───────────────────────────────────────
        double? resistance = FindZone(swingHighs, atr);
        if (resistance.HasValue)
        {
            double r  = resistance.Value;
            bool near = trigger.High >= r - atr * 0.5 &&
                        trigger.High <= r + atr * 0.5;
            if (near)
            {
                double upperWick = trigger.High - Math.Max(trigger.Close, trigger.Open);
                double range     = trigger.High - trigger.Low;
                bool   rejection = range > 0 && upperWick / range > 0.35 && trigger.Close < r;

                if (rejection)
                {
                    double entryLow  = r - atr * 0.3;
                    double entryHigh = r + atr * 0.1;
                    double stopPrice = trigger.High + atr * 0.5;
                    double entryMid  = (entryLow + entryHigh) / 2.0;
                    double risk      = stopPrice - entryMid;
                    if (risk > 0)
                    {
                        double?  sup       = FindZone(swingLows, atr);
                        double   targetHigh = sup.HasValue && sup.Value < trigger.Close
                            ? sup.Value + atr * 0.2
                            : entryMid - risk * MinRR;
                        double   targetLow  = targetHigh - atr;
                        double   reward     = entryMid - targetHigh;

                        if (reward / risk >= MinRR)
                        {
                            return new Signal
                            {
                                Symbol               = symbol,
                                Timeframe            = timeframe,
                                SignalType           = "SRBounce",
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
                    }
                }
            }
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the midpoint of the first two clustered points, or null if none found.</summary>
    private static double? FindZone(List<double> points, double atr)
    {
        if (points.Count < 2) return null;
        var sorted = points.Order().ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i + 1] - sorted[i] <= atr * 0.3)
                return (sorted[i] + sorted[i + 1]) / 2.0;
        }
        return null;
    }

    private static double AverageVolume(IReadOnlyList<Candle> candles, int start, int end)
    {
        int s = Math.Max(0, start);
        int e = Math.Min(candles.Count - 1, end);
        if (e < s) return 0;
        double sum = 0;
        for (int i = s; i <= e; i++) sum += candles[i].Volume;
        return sum / (e - s + 1);
    }
}
