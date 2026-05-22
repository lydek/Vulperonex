using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record DelayAction : WorkflowAction
{
    public const string ActionType = "delay";

    [JsonIgnore]
    public override string Type => ActionType;

    public int DelayMs { get; init; } = 1_000;
}
