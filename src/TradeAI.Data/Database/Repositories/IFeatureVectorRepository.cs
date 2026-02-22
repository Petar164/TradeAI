namespace TradeAI.Data.Database.Repositories;

public interface IFeatureVectorRepository
{
    Task InsertAsync(int signalId, string signalType, string vectorJson, int? outcome = null);
    Task RecordOutcomeAsync(int signalId, int outcome);
    Task<List<(string VectorJson, int? Outcome)>> GetByTypeAsync(string signalType);
}
