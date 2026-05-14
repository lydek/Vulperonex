using System.Security.Cryptography;
using System.Text;

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

        // SPEC §4.2: InvocationId must be stable across TDQ replays.
        // Deriving it deterministically from (EventId, ParentRuleId, ActionIndex)
        // guarantees the same key on every replay without persisting a payload field.
        var invocationId = DeriveInvocationId(
            context.StreamEvent.EventId,
            context.WorkflowRule.Id,
            context.ActionIndex);

        await workflowRuleInvoker.InvokeAsync(
            invokeSubWorkflow.WorkflowId,
            context.StreamEvent,
            invocationId,
            cancellationToken);
    }

    private static string DeriveInvocationId(string eventId, string parentRuleId, int actionIndex)
    {
        var input = $"{eventId}|{parentRuleId}|{actionIndex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // 16 bytes (128 bits) of SHA-256 is sufficient for a collision-resistant
        // dedup discriminator inside the engine and keeps the key compact.
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }
}
