using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Trigger Check-In", "Trigger check-in and activity tracking for a stream viewer")]
public sealed record TriggerCheckInAction : WorkflowAction
{
    public const string ActionType = "triggerCheckIn";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("User ID", "string", required: false, help: "Whose check-in to record. Default {Member.UserId} resolves the triggering user's platform id.", advanced: true)]
    public string UserId { get; init; } = "{Member.UserId}";

    [ActionParam("Platform", "string", required: false, help: "Override stream platform (e.g. twitch). Empty = use the trigger event platform.", advanced: true)]
    public string? Platform { get; init; }
}
