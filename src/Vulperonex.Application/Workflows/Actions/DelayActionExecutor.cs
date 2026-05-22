namespace Vulperonex.Application.Workflows.Actions;

public sealed class DelayActionExecutor : IWorkflowActionExecutor
{
    public string ActionType => DelayAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is DelayAction delayAction)
        {
            await Task.Delay(delayAction.DelayMs, cancellationToken);
        }

        return ActionExecutionResult.Completed;
    }
}
