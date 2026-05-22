namespace Vulperonex.Application.Workflows.Actions;

public interface IWorkflowActionExecutor
{
    string ActionType { get; }

    Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default);
}
