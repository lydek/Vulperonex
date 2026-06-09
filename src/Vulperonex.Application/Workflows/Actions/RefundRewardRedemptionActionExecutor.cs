using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class RefundRewardRedemptionActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver,
    ILogger<RefundRewardRedemptionActionExecutor>? logger = null) : IWorkflowActionExecutor
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
        var hasIds = !string.IsNullOrWhiteSpace(rewardId) && !string.IsNullOrWhiteSpace(redemptionId);

        bool refunded;
        if (string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            // Simulated events must not issue a real Twitch refund (§4.27 Simulation Side-Effect
            // Policy). Skip the Helix call and return a synthetic result so the rest of the rule
            // still runs. External API side effects are never executed under simulation.
            logger?.LogInformation(
                "Refund for reward '{RewardId}' redemption '{RedemptionId}' skipped real Helix call for simulated event; returning synthetic success.",
                rewardId,
                redemptionId);
            refunded = hasIds;
        }
        else
        {
            refunded = hasIds && await helixClient.RefundRedemptionAsync(rewardId, redemptionId, cancellationToken);
        }

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsRefunded"] = refunded,
                ["RewardId"] = rewardId,
                ["RedemptionId"] = redemptionId,
            });
    }
}
