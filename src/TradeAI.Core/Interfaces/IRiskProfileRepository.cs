using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Persistence contract for risk profiles.
/// Implemented in TradeAI.Data; consumed by RiskProfileService in Infrastructure.
/// </summary>
public interface IRiskProfileRepository
{
    /// <summary>Returns the currently active risk profile, or null if none saved.</summary>
    Task<RiskProfile?> GetActiveAsync();

    /// <summary>
    /// Deactivates all existing profiles and inserts the supplied one as active.
    /// </summary>
    Task SaveAsync(RiskProfile profile);
}
