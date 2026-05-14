namespace Vulperonex.Application.Workflows;

/// <summary>
/// At-least-once dedup gate for workflow action execution.
/// TryBeginAsync returns true only if the key has no terminal record
/// (neither Completed nor Failed). Terminal records skip on replay
/// to enforce SPEC §4.2 "Completed/Failed → replay skips" semantics.
/// </summary>
public interface IWorkflowActionExecutionStore
{
    Task<bool> TryBeginAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(ActionExecutionKey key, CancellationToken cancellationToken = default);
}
