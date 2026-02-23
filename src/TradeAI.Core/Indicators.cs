using TradeAI.Core.Models;

namespace TradeAI.Core;

/// <summary>
/// Pure, stateless technical indicator calculations.
///
/// Rules applied consistently across all methods:
///   • Returns <see cref="double.NaN"/> (never throws) when there is
///     insufficient data for the requested <paramref name="period"/>.
///   • Accepts an <see cref="IReadOnlyList{Candle}"/> ordered oldest-first
///     (the same order used by the DB repositories and the cache).
///   • Uses Wilder's smoothing for ATR and RSI (standard convention).
/// </summary>
public static class Indicators
{
    // ── EMA ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exponential moving average of close prices.
    /// Seeded with the SMA of the first <paramref name="period"/> candles.
    /// Returns <see cref="double.NaN"/> when <c>candles.Count &lt; period</c>.
    /// </summary>
    public static double Ema(IReadOnlyList<Candle> candles, int period)
    {
        if (period < 1 || candles.Count < period) return double.NaN;

        double k = 2.0 / (period + 1);

        // Seed with SMA of the first `period` bars
        double ema = 0;
        for (int i = 0; i < period; i++)
            ema += candles[i].Close;
        ema /= period;

        for (int i = period; i < candles.Count; i++)
            ema = candles[i].Close * k + ema * (1 - k);

        return ema;
    }

    // ── ATR ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Average True Range using Wilder's smoothing.
    /// Requires at least <c>period + 1</c> candles (needs a previous close for
    /// the first True Range).  Returns <see cref="double.NaN"/> otherwise.
    /// </summary>
    public static double Atr(IReadOnlyList<Candle> candles, int period)
    {
        if (period < 1 || candles.Count < period + 1) return double.NaN;

        // Seed: plain average of the first `period` true ranges
        double atr = 0;
        for (int i = 1; i <= period; i++)
            atr += TrueRange(candles[i], candles[i - 1].Close);
        atr /= period;

        // Wilder's smoothing for the rest
        for (int i = period + 1; i < candles.Count; i++)
            atr = (atr * (period - 1) + TrueRange(candles[i], candles[i - 1].Close)) / period;

        return atr;
    }

    private static double TrueRange(Candle c, double prevClose)
        => Math.Max(c.High - c.Low,
           Math.Max(Math.Abs(c.High - prevClose),
                    Math.Abs(c.Low  - prevClose)));

    // ── RSI ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Relative Strength Index using Wilder's smoothing.
    /// Requires at least <c>period + 1</c> candles.
    /// Returns <see cref="double.NaN"/> when insufficient; returns 100 when
    /// average loss equals zero (all-up move).
    /// </summary>
    public static double Rsi(IReadOnlyList<Candle> candles, int period)
    {
        if (period < 1 || candles.Count < period + 1) return double.NaN;

        // Seed with simple averages over the first `period` changes
        double avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            double change = candles[i].Close - candles[i - 1].Close;
            if (change > 0) avgGain += change;
            else            avgLoss -= change;  // store positive loss magnitude
        }
        avgGain /= period;
        avgLoss /= period;

        // Wilder's smoothing
        for (int i = period + 1; i < candles.Count; i++)
        {
            double change = candles[i].Close - candles[i - 1].Close;
            double gain   = change > 0 ?  change : 0;
            double loss   = change < 0 ? -change : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss == 0) return 100.0;
        return 100.0 - 100.0 / (1 + avgGain / avgLoss);
    }

    // ── Bollinger Bands ───────────────────────────────────────────────────────

    /// <summary>
    /// Bollinger Bands computed on the trailing <paramref name="period"/> bars.
    /// <para>
    ///   Middle = SMA(period) of close, Upper = Middle + k×σ, Lower = Middle − k×σ
    ///   where σ is the population standard deviation.
    /// </para>
    /// Returns <c>(NaN, NaN, NaN)</c> when <c>candles.Count &lt; period</c>.
    /// </summary>
    public static (double Middle, double Upper, double Lower) BollingerBands(
        IReadOnlyList<Candle> candles, int period, double k = 2.0)
    {
        if (period < 1 || candles.Count < period)
            return (double.NaN, double.NaN, double.NaN);

        int start = candles.Count - period;

        double sum = 0;
        for (int i = start; i < candles.Count; i++)
            sum += candles[i].Close;
        double middle = sum / period;

        double variance = 0;
        for (int i = start; i < candles.Count; i++)
        {
            double d = candles[i].Close - middle;
            variance += d * d;
        }
        double stdDev = Math.Sqrt(variance / period);

        return (middle, middle + k * stdDev, middle - k * stdDev);
    }

    // ── VWAP ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Volume-weighted average price across all supplied candles.
    /// Typical price = (High + Low + Close) / 3.
    /// Returns <see cref="double.NaN"/> when the list is empty or total volume
    /// is zero.
    /// </summary>
    public static double Vwap(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0) return double.NaN;

        double numerator   = 0;
        double denominator = 0;

        foreach (var c in candles)
        {
            double tp = (c.High + c.Low + c.Close) / 3.0;
            numerator   += tp * c.Volume;
            denominator += c.Volume;
        }

        return denominator > 0 ? numerator / denominator : double.NaN;
    }
}
