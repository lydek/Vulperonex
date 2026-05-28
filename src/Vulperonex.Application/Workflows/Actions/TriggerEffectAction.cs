using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Trigger Effect", "Trigger a dynamic effect overlay on stream")]
public sealed record TriggerEffectAction : WorkflowAction
{
    public const string ActionType = "triggerEffect";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Effect ID", "string", required: true, help: "Unique effect identifier to trigger")]
    public required string EffectId { get; init; }

    [ActionParam("Duration (ms)", "number", required: false, help: "Custom display duration in milliseconds")]
    public int? DurationMs { get; init; }
}
