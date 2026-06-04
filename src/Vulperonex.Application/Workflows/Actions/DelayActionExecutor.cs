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
            var delayMs = Math.Max(0, delayAction.DelayMs);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return ActionExecutionResult.Completed;
    }
}
