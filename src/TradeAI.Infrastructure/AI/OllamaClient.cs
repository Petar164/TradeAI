using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace TradeAI.Infrastructure.AI;

/// <summary>
/// Thin wrapper around the Ollama REST API.
/// Streams NDJSON tokens from POST /api/chat.
/// </summary>
public sealed class OllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(HttpClient http) => _http = http;

    /// <summary>
    /// Returns true when Ollama is reachable (GET /api/tags within 2 s).
    /// </summary>
    public async Task<bool> IsAvailableAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var resp = await _http.GetAsync(baseUrl.TrimEnd('/') + "/api/tags", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// Streams chat tokens from POST /api/chat (NDJSON with stream:true).
    /// Each yielded string is a partial token chunk.
    /// </summary>
    public async IAsyncEnumerable<string> StreamChatAsync(
        string baseUrl,
        string model,
        IReadOnlyList<(string Role, string Content)> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            stream = true,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            baseUrl.TrimEnd('/') + "/api/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"),
        };

        HttpResponseMessage? response = null;
        string? sendError = null;
        try
        {
            response = await _http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sendError = ex.Message;
            response?.Dispose();
        }

        if (sendError is not null)
        {
            yield return $"[Ollama error: {sendError}]";
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try { line = await reader.ReadLineAsync(ct); }
            catch (OperationCanceledException) { break; }

            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            string token;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // {"done":true} — stream finished
                if (root.TryGetProperty("done", out var done) && done.GetBoolean()) break;

                if (!root.TryGetProperty("message", out var msg)) continue;
                if (!msg.TryGetProperty("content", out var content)) continue;
                token = content.GetString() ?? string.Empty;
            }
            catch { continue; }

            if (!string.IsNullOrEmpty(token))
                yield return token;
        }

        response.Dispose();
    }
}
