namespace Vulperonex.Application.Workflows;

/// <summary>
/// At-least-once dedup gate for workflow action execution.
/// TryBeginAsync reserves the key for the first caller and returns false
/// for in-flight or terminal records. Completed and Failed records skip on
/// replay to enforce SPEC 4.2 replay semantics.
/// </summary>
public interface IWorkflowActionExecutionStore
{
    Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkAbandonedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);
}
