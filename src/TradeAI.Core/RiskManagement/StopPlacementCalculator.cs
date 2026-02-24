using TradeAI.Core.Models;

namespace TradeAI.Core.RiskManagement;

/// <summary>
/// Computes a stop-loss price from a swing point, ATR, stop style, and trade direction.
///
/// Multipliers:
///   Tight  — ATR × 0.5
///   Normal — ATR × 1.0
///   Wide   — ATR × 1.5
/// </summary>
public static class StopPlacementCalculator
{
    /// <summary>
    /// Returns the stop-loss price.
    /// For LONG trades the stop is placed <em>below</em> the swing point;
    /// for SHORT trades it is placed <em>above</em> the swing point.
    /// </summary>
    /// <param name="swingPoint">The relevant swing low (long) or swing high (short).</param>
    /// <param name="atr">ATR(14) of the current candle window.</param>
    /// <param name="style">The user's chosen stop style.</param>
    /// <param name="isLong">True for long setups, false for short setups.</param>
    public static double Calculate(double swingPoint, double atr, StopStyle style, bool isLong)
    {
        double distance = style switch
        {
            StopStyle.Tight  => atr * 0.5,
            StopStyle.Normal => atr * 1.0,
            StopStyle.Wide   => atr * 1.5,
            _                => atr * 1.0,
        };

        return isLong ? swingPoint - distance : swingPoint + distance;
    }
}
