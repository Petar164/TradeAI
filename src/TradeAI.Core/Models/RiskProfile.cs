namespace TradeAI.Core.Models;

/// <summary>User-configured risk parameters from the onboarding wizard.</summary>
public record RiskProfile
{
    public int               Id                   { get; init; }
    public DateTimeOffset    CreatedAt             { get; init; }
    public double            MaxRiskPct            { get; init; }   // e.g. 1.0 = 1%
    public StopStyle         StopStyle             { get; init; }
    public int               MaxConcurrentTrades   { get; init; }
    public DrawdownTolerance DrawdownTolerance     { get; init; }
    public bool              IsActive              { get; init; }

    /// <summary>ATR multiplier applied to stop distance based on style.</summary>
    public double AtrMultiplier => StopStyle switch
    {
        StopStyle.Tight  => 0.5,
        StopStyle.Normal => 1.0,
        StopStyle.Wide   => 1.5,
        _                => 1.0
    };

    public static RiskProfile Default => new()
    {
        CreatedAt           = DateTimeOffset.UtcNow,
        MaxRiskPct          = 1.0,
        StopStyle           = StopStyle.Normal,
        MaxConcurrentTrades = 3,
        DrawdownTolerance   = DrawdownTolerance.Medium,
        IsActive            = true
    };
}
