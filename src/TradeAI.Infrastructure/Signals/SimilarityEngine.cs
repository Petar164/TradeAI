using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeAI.Core.Interfaces;

namespace TradeAI.Infrastructure.Signals;

/// <summary>
/// kNN-based probability engine.
///
/// On each new signal detection:
///   1. All feature vectors for the same signal type are loaded from the DB.
///   2. Only rows with a recorded outcome (1 = target hit, 0 = stop hit) are used.
///   3. If fewer than <c>MinSamples</c> labelled rows exist, returns <c>null</c>.
///   4. Top-<c>NeighborK</c> nearest neighbours (Euclidean distance) are selected.
///   5. Win rate = hits / K is returned as the probability score.
///
/// Outcomes are recorded asynchronously when the <see cref="OverlayStateMachine"/>
/// publishes a terminal <c>OverlayStateChangedEvent</c>.
/// </summary>
public sealed class SimilarityEngine : ISimilarityEngine
{
    private const int MinSamples = 10;
    private const int NeighborK  = 10;

    private readonly IFeatureVectorStore       _store;
    private readonly ILogger<SimilarityEngine> _logger;

    public SimilarityEngine(IFeatureVectorStore store, ILogger<SimilarityEngine> logger)
    {
        _store  = store;
        _logger = logger;
    }

    // ── Probability computation ────────────────────────────────────────────────

    public async Task<ProbabilityResult?> ComputeProbabilityAsync(
        float[] featureVector, string signalType)
    {
        try
        {
            var rows     = await _store.GetByTypeAsync(signalType);
            var labelled = rows.Where(r => r.Outcome.HasValue).ToList();

            if (labelled.Count < MinSamples) return null;

            // Compute distances to all labelled vectors
            var neighbors = labelled
                .Select(r =>
                {
                    var v2 = Deserialize(r.VectorJson);
                    return (Dist: EuclideanDistance(featureVector, v2), Outcome: r.Outcome!.Value);
                })
                .OrderBy(x => x.Dist)
                .Take(NeighborK)
                .ToList();

            int    hits        = neighbors.Count(x => x.Outcome == 1);
            int    count       = neighbors.Count;
            double hitRate     = (double)hits / count;
            double probability = Math.Round(hitRate * 100, 1);
            int[]  outcomes    = neighbors.Select(x => x.Outcome).ToArray();

            _logger.LogDebug("Similarity [{Type}] → {Pct}% ({Hits}/{K}) from {Total} labelled samples",
                signalType, probability, hits, count, labelled.Count);

            return new ProbabilityResult(probability, count, hitRate, outcomes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SimilarityEngine error computing probability for {Type}", signalType);
            return null;
        }
    }

    // ── Storage ───────────────────────────────────────────────────────────────

    public async Task StoreVectorAsync(int signalId, string signalType, float[] featureVector)
    {
        try
        {
            var json = JsonSerializer.Serialize(featureVector);
            await _store.InsertAsync(signalId, signalType, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SimilarityEngine failed to store vector for signal {Id}", signalId);
        }
    }

    public async Task RecordOutcomeAsync(int signalId, int outcome)
    {
        try
        {
            await _store.RecordOutcomeAsync(signalId, outcome);
            _logger.LogDebug("Outcome recorded: signal {Id} = {Outcome}", signalId, outcome);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SimilarityEngine failed to record outcome for signal {Id}", signalId);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static float[] Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<float[]>(json) ?? []; }
        catch { return []; }
    }

    private static double EuclideanDistance(float[] a, float[] b)
    {
        double sum = 0;
        int    len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            double d = a[i] - b[i];
            sum += d * d;
        }
        return Math.Sqrt(sum);
    }
}
