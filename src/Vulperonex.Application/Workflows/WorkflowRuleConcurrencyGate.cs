using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows;

/// <summary>
/// Process-wide per-rule semaphore registry. Registered as a singleton so that
/// Serial-mode rules are mutually exclusive and Parallel-mode rules respect
/// MaxParallelism <b>across events</b> — the engine itself is scoped (one
/// instance per dispatched event), so engine-local semaphores would only ever
/// guard a single event and never contend.
/// </summary>
public sealed class WorkflowRuleConcurrencyGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the semaphore for the given key, creating it with
    /// <paramref name="capacity"/> on first use. The key includes execution
    /// mode and capacity so an edited rule does not reuse a semaphore sized
    /// for its previous configuration. Superseded entries are not evicted;
    /// the leak is bounded by (rule count × edits per rule × process lifetime)
    /// and is acceptable for the in-process engine.
    /// </summary>
    public SemaphoreSlim GetOrAdd(string key, int capacity)
    {
        return _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(capacity));
    }
}
