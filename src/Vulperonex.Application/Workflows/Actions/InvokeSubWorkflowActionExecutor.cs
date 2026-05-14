namespace Vulperonex.Application.Workflows.Actions;

public sealed class InvokeSubWorkflowActionExecutor(IWorkflowRuleInvoker workflowRuleInvoker) : IWorkflowActionExecutor
{
    public string ActionType => InvokeSubWorkflowAction.ActionType;

    public async Task ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not InvokeSubWorkflowAction invokeSubWorkflow)
        {
            return;
        }

        var invocationId = context.InvocationId ?? Guid.NewGuid().ToString("N");
        await workflowRuleInvoker.InvokeAsync(
            invokeSubWorkflow.WorkflowId,
            context.StreamEvent,
            invocationId,
            cancellationToken);
    }
}
