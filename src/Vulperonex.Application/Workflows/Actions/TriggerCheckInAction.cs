namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record TriggerCheckInAction : WorkflowAction
{
    public const string ActionType = "triggerCheckIn";

    [JsonIgnore]
    public override string Type => ActionType;

    public string UserId { get; init; } = "{Member.UserId}";

    public string? Platform { get; init; }
}
