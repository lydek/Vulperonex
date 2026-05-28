using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Trigger Check-In", "Trigger check-in and activity tracking for a stream viewer")]
public sealed record TriggerCheckInAction : WorkflowAction
{
    public const string ActionType = "triggerCheckIn";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("User ID", "string", required: false, help: "The template expression for the user's ID")]
    public string UserId { get; init; } = "{Member.UserId}";

    [ActionParam("Platform", "string", required: false, help: "The stream platform identifier")]
    public string? Platform { get; init; }
}
