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

    [ActionParam("Target Platform", "string", required: false, help: "Target platform (e.g. twitch, simulation)")]
    public string? TargetPlatform { get; init; }

    [ActionParam("Channel", "string", required: false, help: "Target stream channel name")]
    public string? Channel { get; init; }

    [ActionParam("Deduplication Key", "string", required: false, help: "Deduplication key to prevent duplicate messages")]
    public string? DedupKey { get; init; }
}
