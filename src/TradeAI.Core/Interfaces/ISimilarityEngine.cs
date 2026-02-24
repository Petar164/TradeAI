namespace TradeAI.Core.Interfaces;

/// <summary>
/// Result returned by <see cref="ISimilarityEngine.ComputeProbabilityAsync"/>.
/// </summary>
/// <param name="Probability">Win probability 0–100 (percentage).</param>
/// <param name="SampleCount">Number of top-k neighbours evaluated.</param>
/// <param name="HitRate">Same as Probability / 100 (raw fraction).</param>
/// <param name="NeighborOutcomes">Outcome for each neighbour in distance order: 1=hit, 0=stop.</param>
public sealed record ProbabilityResult(
    double Probability,
    int    SampleCount,
    double HitRate,
    int[]  NeighborOutcomes);

/// <summary>
/// kNN-based probability engine.  Stores feature vectors on signal detection
/// and records binary outcomes when overlays resolve.
/// </summary>
public interface ISimilarityEngine
{
    /// <summary>
    /// Computes win probability for a new signal using Euclidean kNN against
    /// historical feature vectors for the same signal type.
    /// Returns <c>null</c> if fewer than 10 labelled samples exist.
    /// </summary>
    Task<ProbabilityResult?> ComputeProbabilityAsync(float[] featureVector, string signalType);

    /// <summary>Stores the raw feature vector for a newly detected signal (outcome unknown).</summary>
    Task StoreVectorAsync(int signalId, string signalType, float[] featureVector);

    /// <summary>Records the binary outcome (1 = target hit, 0 = stop hit) for a resolved signal.</summary>
    Task RecordOutcomeAsync(int signalId, int outcome);
}
