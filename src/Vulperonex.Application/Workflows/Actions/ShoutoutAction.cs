using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Shoutout", "Send a Twitch Shoutout to a channel")]
public sealed record ShoutoutAction : WorkflowAction
{
    public const string ActionType = "shoutout";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Target Username", "string", required: true, help: "Twitch username to shoutout")]
    public required string TargetLogin { get; init; }
}
