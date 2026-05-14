using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows;

public sealed class InMemoryWorkflowActionExecutionStore : IWorkflowActionExecutionStore
{
    private readonly ConcurrentDictionary<ActionExecutionKey, ActionExecutionStatus> _statusByKey = new();

    public Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_statusByKey.TryAdd(key, ActionExecutionStatus.Running));
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

    private enum ActionExecutionStatus
    {
        Running,
        Completed,
        Failed,
    }
}
