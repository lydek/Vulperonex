using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata(
    "Shoutout",
    "Send a native Twitch /shoutout recommending a channel to your viewers (Helix chat/shoutouts). "
    + "Requires Twitch authorization and a configured broadcaster/moderator id. "
    + "Best-effort: a failed call (not authorized, unknown login) is logged and does NOT abort the rule. "
    + "Note: this triggers Twitch's own shoutout panel only — it does not play a clip on the overlay.")]
public sealed record ShoutoutAction : WorkflowAction
{
    public const string ActionType = "shoutout";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam(
        "Target Username",
        "string",
        required: true,
        help: "Target Twitch login name (not display name). The shoutout is skipped if the user cannot be found.")]
    public required string TargetLogin { get; init; }
}
