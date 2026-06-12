using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows;

/// <summary>
/// Idempotency ledger for action executions. Replays (e.g. from the transient
/// delivery queue) must skip actions that already reached a terminal state, so
/// the store has to outlive a single event scope — it is registered as a
/// singleton. Entries are evicted FIFO once <see cref="MaxEntries"/> is
/// exceeded, which keeps memory bounded for a long-running desktop process;
/// replay protection only needs to cover the recent event window anyway.
/// </summary>
public sealed class InMemoryWorkflowActionExecutionStore : IWorkflowActionExecutionStore
{
    public const int DefaultMaxEntries = 50_000;

    private readonly ConcurrentDictionary<ActionExecutionKey, ActionExecutionStatus> _statusByKey = new();
    private readonly ConcurrentQueue<ActionExecutionKey> _insertionOrder = new();
    private readonly int _maxEntries;

    public InMemoryWorkflowActionExecutionStore(int maxEntries = DefaultMaxEntries)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        _maxEntries = maxEntries;
    }

    public int MaxEntries => _maxEntries;

    public Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_statusByKey.TryAdd(key, ActionExecutionStatus.Running))
        {
            return Task.FromResult(false);
        }

        _insertionOrder.Enqueue(key);
        EvictOverflow();
        return Task.FromResult(true);
    }

    public Task MarkCompletedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _statusByKey[key] = ActionExecutionStatus.Completed;
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _statusByKey[key] = ActionExecutionStatus.Failed;
        return Task.CompletedTask;
    }

    public Task MarkSkippedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _statusByKey[key] = ActionExecutionStatus.Skipped;
        return Task.CompletedTask;
    }

    public Task MarkAbandonedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _statusByKey.TryRemove(
            new KeyValuePair<ActionExecutionKey, ActionExecutionStatus>(key, ActionExecutionStatus.Running));
        return Task.CompletedTask;
    }

    private void EvictOverflow()
    {
        // Oldest-first eviction; a Running entry can in theory be evicted, but
        // at 50k retained entries that would require an action still executing
        // after tens of thousands of newer actions began — acceptable.
        while (_statusByKey.Count > _maxEntries && _insertionOrder.TryDequeue(out var oldest))
        {
            _statusByKey.TryRemove(oldest, out _);
        }
    }

    private enum ActionExecutionStatus
    {
        Running,
        Completed,
        Failed,
        Skipped,
    }
}
