using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Update Counter", "Increment or decrement a persistent named counter")]
public sealed record UpdateCounterAction : WorkflowAction
{
    public const string ActionType = "updateCounter";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Counter Key", "string", required: true, help: "Persistent tracker key for the counter")]
    public required string Key { get; init; }

    [ActionParam("Delta Value", "number", required: false, help: "Value offset to apply, default is 1")]
    public long Delta { get; init; } = 1;
}
