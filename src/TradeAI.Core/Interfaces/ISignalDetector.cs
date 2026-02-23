using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Contract for a single signal-detection algorithm.
/// Implementations receive the full candle history and return a Signal
/// if a valid setup is detected, or null otherwise.
/// </summary>
public interface ISignalDetector
{
    /// <summary>
    /// Inspect <paramref name="candles"/> (oldest-first) and return a Signal
    /// if a setup is found, or <c>null</c> if no conditions are met.
    /// Must never throw.
    /// </summary>
    Task<Signal?> DetectAsync(IReadOnlyList<Candle> candles, string symbol, string timeframe);
}
