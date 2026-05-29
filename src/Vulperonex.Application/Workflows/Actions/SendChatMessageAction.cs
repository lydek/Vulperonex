using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Send Chat Message", "Send a message to a stream platform chat")]
public sealed record SendChatMessageAction : WorkflowAction
{
    public const string ActionType = "sendChatMessage";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Template", "string", required: true, help: "Message content template using bracket variable notation")]
    public required string Template { get; init; }

    // Cross-platform routing is not exposed in the editor; defaults to the triggering event's platform.
    // Property retained for plugin/internal routing and raw-JSON overrides.
    public string? TargetPlatform { get; init; }

    // Channel routing is not exposed in the editor; defaults to the globally configured channel.
    // Property retained for plugin/internal routing and raw-JSON overrides.
    public string? Channel { get; init; }

    [ActionParam("Deduplication Key", "string", required: false, help: "Deduplication key to prevent duplicate messages")]
    public string? DedupKey { get; init; }
}
