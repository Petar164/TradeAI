namespace TradeAI.Core.Models;

/// <summary>One OHLCV candle for a symbol + timeframe.</summary>
public record Candle(
    int             Id,
    string          Symbol,
    string          Timeframe,
    DateTimeOffset  OpenTime,
    double          Open,
    double          High,
    double          Low,
    double          Close,
    double          Volume
)
{
    /// <summary>Convenience: mid-price of the candle body.</summary>
    public double MidPrice => (Open + Close) / 2.0;

    /// <summary>True candle range (high - low).</summary>
    public double Range => High - Low;

    /// <summary>Body size (absolute).</summary>
    public double BodySize => Math.Abs(Close - Open);

    public bool IsBullish => Close >= Open;
}
