using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class TriggerCheckInActionExecutor(
    IMemberStreamStateRepository streamStateRepository,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => TriggerCheckInAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not TriggerCheckInAction triggerCheckInAction)
        {
            return ActionExecutionResult.Completed;
        }

        var userId = templateResolver.Resolve(triggerCheckInAction.UserId, context.ExpressionContext);
        var platform = string.IsNullOrWhiteSpace(triggerCheckInAction.Platform)
            ? context.StreamEvent.Platform
            : templateResolver.Resolve(triggerCheckInAction.Platform, context.ExpressionContext);

        var count = await streamStateRepository.IncrementCheckInAsync(
            PlatformIdentity.Create(platform, userId),
            cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Platform"] = platform,
                ["UserId"] = userId,
                ["CheckInCount"] = count,
            });
    }
}
