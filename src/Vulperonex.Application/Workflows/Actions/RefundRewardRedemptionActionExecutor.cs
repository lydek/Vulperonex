using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class RefundRewardRedemptionActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => RefundRewardRedemptionAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not RefundRewardRedemptionAction refundAction)
        {
            return ActionExecutionResult.Completed;
        }

        var rewardId = templateResolver.Resolve(refundAction.RewardId, context.ExpressionContext).Trim();
        var redemptionId = templateResolver.Resolve(refundAction.RedemptionId, context.ExpressionContext).Trim();
        var refunded = !string.IsNullOrWhiteSpace(rewardId)
            && !string.IsNullOrWhiteSpace(redemptionId)
            && await helixClient.RefundRedemptionAsync(rewardId, redemptionId, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsRefunded"] = refunded,
                ["RewardId"] = rewardId,
                ["RedemptionId"] = redemptionId,
            });
    }
}
