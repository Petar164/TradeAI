using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Manages the user's active risk profile: loads it from the DB, saves updates,
/// and applies natural-language override commands from the chat bar.
/// </summary>
public interface IRiskProfileService
{
    /// <summary>The currently active risk profile (never null; falls back to Default).</summary>
    RiskProfile Current { get; }

    /// <summary>Load the active profile from the database. Called once on startup.</summary>
    Task LoadAsync();

    /// <summary>Persist a profile as the new active profile and update <see cref="Current"/>.</summary>
    Task SaveAsync(RiskProfile profile);

    /// <summary>
    /// Applies a natural-language override command ("too risky", "more aggressive",
    /// "tighten stops", "widen stops", "reset").
    /// Updates <see cref="Current"/> in-memory and publishes <c>RiskProfileUpdatedEvent</c>.
    /// Returns a confirmation message to display in the chat bar, or null if not recognised.
    /// </summary>
    string? ApplyOverride(string command);
}
