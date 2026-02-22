namespace TradeAI.Core.Models;

/// <summary>
/// An immutable trade setup detected by a signal detector.
/// All overlay state transitions produce a new Signal via `with`.
/// </summary>
public record Signal
{
    public int              Id                      { get; init; }
    public required string  Symbol                  { get; init; }
    public required string  Timeframe               { get; init; }
    public required string  SignalType              { get; init; }  // "TrendContinuation" etc.
    public TradeDirection   Direction               { get; init; }
    public DateTimeOffset   DetectedAtCandleTime    { get; init; }

    // Zone coordinates
    public double           EntryLow                { get; init; }
    public double           EntryHigh               { get; init; }
    public double           StopPrice               { get; init; }
    public double           TargetLow               { get; init; }
    public double           TargetHigh              { get; init; }
    public int              TtlCandles              { get; init; }

    // Probability (filled by SimilarityEngine)
    public double?          ConfidencePct           { get; init; }
    public int?             SimilaritySampleCount   { get; init; }
    public double?          HistoricalHitRatePct    { get; init; }

    // State machine
    public OverlayState     State                   { get; init; } = OverlayState.Pending;
    public DateTimeOffset?  OutcomeTime             { get; init; }

    // Feature vector (JSON blob, stored for kNN lookups)
    public string?          FeatureVectorJson       { get; init; }

    // ── Derived helpers ────────────────────────────────────────
    public double EntryMid   => (EntryLow + EntryHigh) / 2.0;
    public double TargetMid  => (TargetLow + TargetHigh) / 2.0;
    public double RiskPoints => Math.Abs(EntryMid - StopPrice);
    public double RewardPoints => Math.Abs(TargetMid - EntryMid);
    public double RRatio     => RiskPoints > 0 ? RewardPoints / RiskPoints : 0;

    /// <summary>Live R-multiple given current price.</summary>
    public double RMultiple(double currentPrice)
    {
        if (RiskPoints <= 0) return 0;
        var pnl = Direction == TradeDirection.Long
            ? currentPrice - EntryMid
            : EntryMid - currentPrice;
        return pnl / RiskPoints;
    }
}
