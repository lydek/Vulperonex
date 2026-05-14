namespace Vulperonex.Application.Workflows.Actions;

public interface IWorkflowActionExecutor
{
    string ActionType { get; }

    Task ExecuteAsync(WorkflowAction action, ActionExecutionContext context, CancellationToken cancellationToken = default);
}
