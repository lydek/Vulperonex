using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record StopIfAction : WorkflowAction
{
    public const string ActionType = "stopIf";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string Condition { get; init; }
}
