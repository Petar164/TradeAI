namespace TradeAI.Core.Interfaces;

/// <summary>
/// Write/read interface for feature vectors — placed in Core so Infrastructure
/// can persist and query feature vectors without referencing TradeAI.Data directly.
/// </summary>
public interface IFeatureVectorStore
{
    /// <summary>Inserts a new feature vector row (no outcome yet).</summary>
    Task InsertAsync(int signalId, string signalType, string vectorJson, int? outcome = null);

    /// <summary>Records the binary outcome (1 = target hit, 0 = stop hit) for a resolved signal.</summary>
    Task RecordOutcomeAsync(int signalId, int outcome);

    /// <summary>Returns all stored vectors for a given signal type, newest-first.</summary>
    Task<List<(string VectorJson, int? Outcome)>> GetByTypeAsync(string signalType);
}
