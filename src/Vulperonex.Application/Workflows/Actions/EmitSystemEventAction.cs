using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Emit System Event", "Emit a custom system event into the event bus")]
public sealed record EmitSystemEventAction : WorkflowAction
{
    public const string ActionType = "emitSystemEvent";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Event Type Key", "string", required: true, help: "Unique identifier key for the event type")]
    public required string EventTypeKey { get; init; }

    [ActionParam("Payload", "dictionary", required: false, help: "Key-value pair dictionary payload for the system event")]
    public IReadOnlyDictionary<string, string> Payload { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
