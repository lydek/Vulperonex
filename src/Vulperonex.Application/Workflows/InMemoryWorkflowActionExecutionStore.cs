using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows;

public sealed class InMemoryWorkflowActionExecutionStore : IWorkflowActionExecutionStore
{
    private readonly ConcurrentDictionary<ActionExecutionKey, bool> _completedKeys = new();

    public Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!_completedKeys.ContainsKey(key));
    }

    public Task MarkCompletedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _completedKeys[key] = true;
        return Task.CompletedTask;
    }
}
