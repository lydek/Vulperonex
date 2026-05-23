using System.Security.Cryptography;
using System.Text;
using Vulperonex.Application.Expressions;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class InvokeSubWorkflowActionExecutor(
    Func<IWorkflowRuleInvoker> workflowRuleInvokerFactory,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => InvokeSubWorkflowAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not InvokeSubWorkflowAction invokeSubWorkflow)
        {
            return ActionExecutionResult.Completed;
        }

        // SPEC §4.2: InvocationId must be stable across TDQ replays.
        // Deriving it deterministically from (EventId, ParentRuleId, ActionIndex)
        // guarantees the same key on every replay without persisting a payload field.
        var invocationId = DeriveInvocationId(
            context.StreamEvent.EventId,
            context.WorkflowRule.Id,
            context.ActionIndex);

        await workflowRuleInvokerFactory().InvokeAsync(
            invokeSubWorkflow.WorkflowId,
            context.StreamEvent,
            invocationId,
            ResolveArgs(invokeSubWorkflow.Args, context.ExpressionContext),
            cancellationToken);
        return ActionExecutionResult.Completed;
    }

    private IReadOnlyDictionary<string, string> ResolveArgs(
        IReadOnlyDictionary<string, string> args,
        ExpressionContext expressionContext)
    {
        return args.ToDictionary(
            pair => pair.Key,
            pair => templateResolver.Resolve(pair.Value, expressionContext),
            StringComparer.OrdinalIgnoreCase);
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
