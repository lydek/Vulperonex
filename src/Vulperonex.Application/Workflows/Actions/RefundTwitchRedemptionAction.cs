using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Refund Twitch Redemption", "Refund channel point redemption back to Twitch user")]
public sealed record RefundTwitchRedemptionAction : WorkflowAction
{
    public const string ActionType = "refundTwitchRedemption";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Reward ID", "string", required: false, help: "Twitch channel reward ID")]
    public string RewardId { get; init; } = "{Trigger.RewardId}";

    [ActionParam("Redemption ID", "string", required: false, help: "Twitch channel reward redemption ID")]
    public string RedemptionId { get; init; } = "{Trigger.RedemptionId}";
}
