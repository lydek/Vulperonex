namespace Vulperonex.Application.Workflows;

public interface IWorkflowActionExecutionStore
{
    Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);
}
