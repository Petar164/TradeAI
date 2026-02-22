namespace TradeAI.Infrastructure.MarketData;

/// <summary>
/// Token-bucket rate limiter with a three-state circuit breaker.
///
/// States:  Closed (normal) → Open (pause all, N consecutive failures)
///                          → HalfOpen (allow one probe after cooldown)
///                          → Closed (probe succeeded)
///
/// Default: 10-token bucket, refill 1 token/second → ~60 req/min peak.
/// </summary>
public sealed class RateLimitScheduler
{
    // Token bucket
    private readonly SemaphoreSlim _lock = new(1, 1);
    private double   _tokens;
    private readonly double _maxTokens;
    private readonly double _tokensPerSecond;
    private DateTime _lastRefill;

    // Circuit breaker
    private int       _consecutiveFailures;
    private DateTime? _openUntil;
    private bool      _halfOpenProbeAllowed;
    private const int    FailureThreshold     = 3;
    private const double CircuitBreakSeconds  = 60.0;

    public RateLimitScheduler(double maxTokens = 10, double tokensPerSecond = 1.0)
    {
        _tokens          = maxTokens;
        _maxTokens        = maxTokens;
        _tokensPerSecond  = tokensPerSecond;
        _lastRefill       = DateTime.UtcNow;
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public bool IsOpen =>
        _openUntil.HasValue && DateTime.UtcNow < _openUntil.Value && !_halfOpenProbeAllowed;

    /// <summary>Block until a token is available (respects circuit breaker).</summary>
    public async Task AcquireAsync(CancellationToken ct = default)
    {
        // Circuit breaker: Open → HalfOpen transition
        if (_openUntil.HasValue && DateTime.UtcNow >= _openUntil.Value)
        {
            _halfOpenProbeAllowed = true;
            _openUntil = null;
        }

        if (IsOpen)
            throw new InvalidOperationException(
                $"Circuit breaker is open. Retrying after {_openUntil:HH:mm:ss}.");

        // Wait for a token
        while (true)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    _halfOpenProbeAllowed = false;
                    break;
                }
            }
            finally { _lock.Release(); }

            await Task.Delay(500, ct);
        }

        // Jitter to spread bursts
        await Task.Delay(Random.Shared.Next(0, 250), ct);
    }

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        _halfOpenProbeAllowed = false;
    }

    public void RecordFailure()
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        _halfOpenProbeAllowed = false;
        if (failures >= FailureThreshold)
            _openUntil = DateTime.UtcNow.AddSeconds(CircuitBreakSeconds);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Refill()
    {
        var now     = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _tokens     = Math.Min(_maxTokens, _tokens + elapsed * _tokensPerSecond);
        _lastRefill = now;
    }
}
