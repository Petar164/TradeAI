using TradeAI.Core.Interfaces;

namespace TradeAI.Data.Database.Repositories;

/// <summary>
/// Extends <see cref="IFeatureVectorStore"/> with any Data-layer-specific members.
/// Currently identical — the base interface is used by Infrastructure via DI forwarding.
/// </summary>
public interface IFeatureVectorRepository : IFeatureVectorStore
{
    // All members inherited from IFeatureVectorStore.
}
