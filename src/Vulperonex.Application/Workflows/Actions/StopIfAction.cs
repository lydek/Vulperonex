using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Stop If", "Short-circuit and halt workflow execution if a condition is met")]
public sealed record StopIfAction : WorkflowAction
{
    public const string ActionType = "stopIf";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Condition", "string", required: true, help: "Boolean expression condition evaluated by NCalc")]
    public required string Condition { get; init; }
}
