namespace TradeAI.Core.Models;

/// <summary>
/// Defines the entry zone, stop, and target for a trade setup.
/// Attached to a Signal before drawing the overlay.
/// </summary>
public record TradeZone(
    double EntryLow,
    double EntryHigh,
    double StopPrice,
    double TargetLow,
    double TargetHigh,
    int    TtlCandles
)
{
    /// <summary>Risk = distance from entry midpoint to stop.</summary>
    public double RiskPoints => Math.Abs((EntryLow + EntryHigh) / 2.0 - StopPrice);

    /// <summary>Reward = distance from entry midpoint to target midpoint.</summary>
    public double RewardPoints => Math.Abs((TargetLow + TargetHigh) / 2.0 - (EntryLow + EntryHigh) / 2.0);

    /// <summary>Reward-to-risk ratio. 0 if risk is zero.</summary>
    public double RRatio => RiskPoints > 0 ? RewardPoints / RiskPoints : 0;
}
