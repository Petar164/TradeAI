using TradeAI.Core.Interfaces;
using TradeAI.Core.Messaging;
using TradeAI.Core.Messaging.Events;
using TradeAI.Core.Models;

namespace TradeAI.Infrastructure.RiskManagement;

/// <summary>
/// Manages the user's active <see cref="RiskProfile"/>.
///
/// • LoadAsync — hydrates <see cref="Current"/> from SQLite on startup
/// • SaveAsync — persists a new profile and publishes <see cref="RiskProfileUpdatedEvent"/>
/// • ApplyOverride — interprets a natural-language command from the chat bar,
///   mutates <see cref="Current"/> in-memory, and publishes the update event.
///   Returns a confirmation string, or null if the command wasn't recognised.
/// </summary>
public sealed class RiskProfileService : IRiskProfileService
{
    private readonly IRiskProfileRepository _repo;
    private readonly SignalBus              _bus;

    private RiskProfile _current = RiskProfile.Default;
    public  RiskProfile Current  => _current;

    public RiskProfileService(IRiskProfileRepository repo, SignalBus bus)
    {
        _repo = repo;
        _bus  = bus;
    }

    public async Task LoadAsync()
    {
        var saved = await _repo.GetActiveAsync();
        if (saved is not null)
            _current = saved;
    }

    public async Task SaveAsync(RiskProfile profile)
    {
        await _repo.SaveAsync(profile);
        _current = profile;
        _bus.Publish(new RiskProfileUpdatedEvent(profile));
    }

    public string? ApplyOverride(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(cmd)) return null;

        // ── Reset ──────────────────────────────────────────────────────────────
        if (cmd.Contains("reset") || cmd.Contains("default"))
        {
            _ = LoadAsync();   // fire-and-forget reload from DB
            _bus.Publish(new RiskProfileUpdatedEvent(_current));
            return "Risk profile reset to saved settings.";
        }

        // ── Reduce risk ────────────────────────────────────────────────────────
        if (cmd.Contains("too risky") || cmd.Contains("less risk") || cmd.Contains("reduce risk"))
        {
            StopStyle newStop = _current.StopStyle switch
            {
                StopStyle.Tight  => StopStyle.Normal,
                StopStyle.Normal => StopStyle.Wide,
                _                => StopStyle.Wide,
            };
            double newRisk = Math.Max(0.25, Math.Round(_current.MaxRiskPct * 0.75, 2));
            _current = _current with { StopStyle = newStop, MaxRiskPct = newRisk };
            _bus.Publish(new RiskProfileUpdatedEvent(_current));
            return $"Stop widened to {newStop}, max risk reduced to {newRisk:F2}% per trade.";
        }

        // ── Increase risk ──────────────────────────────────────────────────────
        if (cmd.Contains("aggressive") || cmd.Contains("more risk") || cmd.Contains("increase risk"))
        {
            StopStyle newStop = _current.StopStyle switch
            {
                StopStyle.Wide   => StopStyle.Normal,
                StopStyle.Normal => StopStyle.Tight,
                _                => StopStyle.Tight,
            };
            _current = _current with { StopStyle = newStop };
            _bus.Publish(new RiskProfileUpdatedEvent(_current));
            return $"Stop tightened to {newStop}. Higher risk mode active — trade carefully.";
        }

        // ── Tighten stops ──────────────────────────────────────────────────────
        if (cmd.Contains("tighten stop"))
        {
            StopStyle newStop = _current.StopStyle switch
            {
                StopStyle.Wide   => StopStyle.Normal,
                StopStyle.Normal => StopStyle.Tight,
                _                => StopStyle.Tight,
            };
            _current = _current with { StopStyle = newStop };
            _bus.Publish(new RiskProfileUpdatedEvent(_current));
            return $"Stop style set to {newStop}.";
        }

        // ── Widen stops ────────────────────────────────────────────────────────
        if (cmd.Contains("widen stop"))
        {
            StopStyle newStop = _current.StopStyle switch
            {
                StopStyle.Tight  => StopStyle.Normal,
                StopStyle.Normal => StopStyle.Wide,
                _                => StopStyle.Wide,
            };
            _current = _current with { StopStyle = newStop };
            _bus.Publish(new RiskProfileUpdatedEvent(_current));
            return $"Stop style set to {newStop}.";
        }

        return null;
    }
}
