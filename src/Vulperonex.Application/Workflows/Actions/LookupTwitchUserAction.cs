using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Lookup Twitch User", "Lookup user profile and details from Twitch API")]
public sealed record LookupTwitchUserAction : WorkflowAction
{
    public const string ActionType = "lookupTwitchUser";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Username", "string", required: false, help: "Twitch username (login) to search")]
    public string? Login { get; init; }

    [ActionParam("User ID", "string", required: false, help: "Twitch unique user ID to search")]
    public string? UserId { get; init; }
}
