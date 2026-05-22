namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record RefundTwitchRedemptionAction : WorkflowAction
{
    public const string ActionType = "refundTwitchRedemption";

    [JsonIgnore]
    public override string Type => ActionType;

    public string RewardId { get; init; } = "{Trigger.RewardId}";

    public string RedemptionId { get; init; } = "{Trigger.RedemptionId}";
}
