using TradeAI.Core.Models;

namespace TradeAI.Core.Interfaces;

/// <summary>
/// Context-aware AI trading assistant powered by a local Ollama model.
/// Streams token-by-token responses and maintains conversation history.
/// </summary>
public interface IAIAssistant
{
    /// <summary>True when Ollama is reachable at the configured base URL.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Ask the assistant a question. Yields response tokens as they stream in.
    /// Call <see cref="SetContext"/> before asking so the system prompt reflects
    /// the current chart state.
    /// </summary>
    IAsyncEnumerable<string> AskAsync(string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Update the trading context injected into the system prompt.
    /// Should be called whenever a new signal is detected or the symbol/timeframe changes.
    /// </summary>
    void SetContext(Signal? latestSignal, RiskProfile riskProfile, string symbol, string timeframe);

    /// <summary>Clear conversation history (new session).</summary>
    void ClearHistory();
}
