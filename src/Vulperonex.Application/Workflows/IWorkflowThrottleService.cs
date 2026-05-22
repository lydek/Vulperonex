using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

public interface IWorkflowThrottleService
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        WorkflowRule rule,
        IStreamEvent streamEvent,
        CancellationToken cancellationToken = default);
}
