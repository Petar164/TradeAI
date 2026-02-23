using System.Collections.Concurrent;

namespace TradeAI.Core.Messaging;

/// <summary>
/// Lightweight, type-safe, in-process pub/sub bus.
///
/// Subscribers hand in an <see cref="Action{T}"/> and receive an
/// <see cref="IDisposable"/> token. Disposing the token unsubscribes.
///
/// Internally the bus stores <see cref="WeakReference{T}"/> to each
/// handler so a forgotten subscription never causes a memory leak — the
/// subscriber must still hold the token to keep the handler alive.
///
/// If a non-null <paramref name="uiContext"/> is supplied at construction
/// all handler invocations are posted onto that context (useful for
/// updating WPF ViewModels from a background thread without Dispatcher
/// calls in every subscriber).
/// </summary>
public sealed class SignalBus
{
    // Type → live subscriber entries
    private readonly ConcurrentDictionary<Type, List<Entry>> _map = new();
    private readonly SynchronizationContext?                 _uiContext;

    public SignalBus(SynchronizationContext? uiContext = null)
        => _uiContext = uiContext;

    // ── Subscribe ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribe to messages of type <typeparamref name="T"/>.
    /// Dispose the returned token to unsubscribe.
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var list  = _map.GetOrAdd(typeof(T), _ => new List<Entry>());
        var entry = new Entry(handler);

        lock (list) { list.Add(entry); }

        return new Token(() =>
        {
            lock (list) { list.Remove(entry); }
        });
    }

    // ── Publish ───────────────────────────────────────────────────────────────

    /// <summary>Publish a message to all live subscribers.</summary>
    public void Publish<T>(T message)
    {
        if (!_map.TryGetValue(typeof(T), out var list)) return;

        List<Entry> snapshot;
        lock (list) { snapshot = new List<Entry>(list); }

        var dead = new List<Entry>();

        foreach (var entry in snapshot)
        {
            if (!entry.Weak.TryGetTarget(out var del))
            {
                dead.Add(entry);
                continue;
            }

            var action = (Action<T>)del;

            if (_uiContext != null)
                _uiContext.Post(_ => action(message), null);
            else
                action(message);
        }

        if (dead.Count > 0)
            lock (list) { foreach (var d in dead) list.Remove(d); }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed class Entry(Delegate handler)
    {
        public WeakReference<Delegate> Weak { get; } = new(handler);
    }

    private sealed class Token(Action onDispose) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                onDispose();
        }
    }
}
