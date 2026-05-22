using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record UpdateCounterAction : WorkflowAction
{
    public const string ActionType = "updateCounter";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string Key { get; init; }

    public long Delta { get; init; } = 1;
}
