using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Write-side of the signal repository — placed in Core so Infrastructure
/// can persist signals without referencing TradeAI.Data.
/// </summary>
public interface ISignalStore
{
    /// <summary>Inserts a new signal and returns its DB-assigned id.</summary>
    Task<int> InsertAsync(Signal signal);

    /// <summary>Updates the overlay state and optional outcome timestamp.</summary>
    Task UpdateStateAsync(int id, OverlayState state, DateTimeOffset? outcomeTime = null);
}
