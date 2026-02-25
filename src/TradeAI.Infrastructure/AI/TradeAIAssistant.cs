using System.Text;
using TradeAI.Core.Interfaces;
using TradeAI.Core.Models;
using TradeAI.Infrastructure.Settings;

namespace TradeAI.Infrastructure.AI;

/// <summary>
/// Context-aware AI trading assistant powered by a local Ollama model.
/// Streams token-by-token responses and maintains conversation history.
/// </summary>
public sealed class TradeAIAssistant : IAIAssistant
{
    private const int MaxHistoryMessages = 40;   // 20 exchanges

    private readonly OllamaClient _client;
    private readonly AppSettings  _settings;

    // Mutable context — updated by SetContext before each question
    private Signal?       _latestSignal;
    private RiskProfile   _riskProfile = RiskProfile.Default;
    private string        _symbol      = "AAPL";
    private string        _timeframe   = "5m";

    private readonly List<(string Role, string Content)> _history = [];
    private bool _available;
    private bool _checked;

    public bool IsAvailable => _available;

    public TradeAIAssistant(OllamaClient client, AppSettings settings)
    {
        _client   = client;
        _settings = settings;
    }

    // ── IAIAssistant ─────────────────────────────────────────────────────────

    public void SetContext(Signal? latestSignal, RiskProfile riskProfile,
                           string symbol, string timeframe)
    {
        _latestSignal = latestSignal;
        _riskProfile  = riskProfile;
        _symbol       = symbol;
        _timeframe    = timeframe;
    }

    public void ClearHistory() => _history.Clear();

    public async IAsyncEnumerable<string> AskAsync(string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        // Lazy availability check
        if (!_checked)
        {
            _available = await _client.IsAvailableAsync(_settings.OllamaBaseUrl, ct);
            _checked   = true;
        }

        if (!_available)
        {
            yield return "Ollama is not running. Start it with `ollama serve` and ensure " +
                         $"the model '{_settings.OllamaModel}' is pulled.";
            yield break;
        }

        // Build the message list: system + history + new user message
        var messages = new List<(string Role, string Content)>
        {
            ("system", BuildSystemPrompt()),
        };
        messages.AddRange(_history);
        messages.Add(("user", userMessage));

        // Collect the streamed response so we can append it to history
        var sb = new StringBuilder();

        await foreach (var token in _client.StreamChatAsync(
            _settings.OllamaBaseUrl, _settings.OllamaModel, messages, ct))
        {
            sb.Append(token);
            yield return token;
        }

        // Persist the exchange to history (capped)
        _history.Add(("user",      userMessage));
        _history.Add(("assistant", sb.ToString()));
        while (_history.Count > MaxHistoryMessages)
            _history.RemoveAt(0);
    }

    // ── System prompt builder ─────────────────────────────────────────────────

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are TradeAI Assistant, an expert trading coach embedded in a WPF desktop application.");
        sb.AppendLine("You help traders understand signal quality, risk management, and trade planning.");
        sb.AppendLine("Keep answers concise, practical, and specific to the current context below.");
        sb.AppendLine("Never recommend specific stocks to buy/sell as investment advice; always frame as analysis.");
        sb.AppendLine();

        sb.AppendLine($"## Current Chart Context");
        sb.AppendLine($"- Symbol: {_symbol}");
        sb.AppendLine($"- Timeframe: {_timeframe}");
        sb.AppendLine();

        sb.AppendLine("## Risk Profile");
        sb.AppendLine($"- Max risk per trade: {_riskProfile.MaxRiskPct}%");
        sb.AppendLine($"- Stop style: {_riskProfile.StopStyle} (0.5× / 1.0× / 1.5× ATR)");
        sb.AppendLine($"- Max concurrent trades: {_riskProfile.MaxConcurrentTrades}");
        sb.AppendLine($"- Drawdown tolerance: {_riskProfile.DrawdownTolerance}");
        sb.AppendLine();

        if (_latestSignal is not null)
        {
            var s = _latestSignal;
            sb.AppendLine("## Latest Detected Signal");
            sb.AppendLine($"- Type: {s.SignalType}");
            sb.AppendLine($"- Direction: {s.Direction}");
            sb.AppendLine($"- Entry zone: {s.EntryLow:F4} – {s.EntryHigh:F4}");
            sb.AppendLine($"- Stop price: {s.StopPrice:F4}");
            sb.AppendLine($"- Target: {s.TargetLow:F4} – {s.TargetHigh:F4}");
            sb.AppendLine($"- R:R ratio: {s.RRatio:F2}");
            sb.AppendLine($"- TTL: {s.TtlCandles} candles");
            if (s.ConfidencePct.HasValue)
            {
                sb.AppendLine($"- Win probability: {s.ConfidencePct:F0}% " +
                              $"(based on {s.SimilaritySampleCount ?? 0} similar historical setups)");
            }
        }
        else
        {
            sb.AppendLine("## Latest Detected Signal");
            sb.AppendLine("- No signal currently detected. Discuss general analysis or past signals.");
        }

        return sb.ToString();
    }
}
