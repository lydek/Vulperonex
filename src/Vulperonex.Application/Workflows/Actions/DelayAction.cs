using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Delay", "Delay workflow execution for a specified duration")]
public sealed record DelayAction : WorkflowAction
{
    public const string ActionType = "delay";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Delay (ms)", "number", required: false, help: "Duration in milliseconds to delay execution")]
    public int DelayMs { get; init; } = 1_000;
}
