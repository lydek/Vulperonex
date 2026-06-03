using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

/// <summary>
/// Look up a platform user's profile (login, display name, avatar) via the
/// configured <see cref="Vulperonex.Application.Twitch.IHelixClient"/>. The
/// type discriminator is preserved as <c>"lookupTwitchUser"</c> for backward
/// compatibility with rules saved before the type was renamed to be
/// platform-neutral (see SPEC §6.1).
/// </summary>
[ActionMetadata("Lookup Platform User", "Look up user profile and details from the platform API")]
public sealed record LookupPlatformUserAction : WorkflowAction
{
    public const string ActionType = "lookupTwitchUser";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Username", "string", required: false, help: "Platform username (login) to search")]
    public string? Login { get; init; }

    [ActionParam("User ID", "string", required: false, help: "Platform unique user ID to search")]
    public string? UserId { get; init; }
}
