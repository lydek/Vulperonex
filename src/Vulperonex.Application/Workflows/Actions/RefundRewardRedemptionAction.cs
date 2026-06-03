using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

/// <summary>
/// Refund a channel-point reward redemption. The type discriminator is preserved
/// as <c>"refundTwitchRedemption"</c> for backward compatibility with rules
/// saved before the type was renamed to be platform-neutral (see SPEC §6.1).
/// </summary>
[ActionMetadata("Refund Reward Redemption", "Refund a channel-point reward redemption back to the platform user")]
public sealed record RefundRewardRedemptionAction : WorkflowAction
{
    public const string ActionType = "refundTwitchRedemption";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Reward ID", "string", required: false, help: "Channel-point reward ID")]
    public string RewardId { get; init; } = "{Trigger.RewardId}";

    [ActionParam("Redemption ID", "string", required: false, help: "Channel-point reward redemption ID")]
    public string RedemptionId { get; init; } = "{Trigger.RedemptionId}";
}
