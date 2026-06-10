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
[ActionMetadata(
    "Lookup Platform User",
    "Resolve a login or display name (e.g. viewer_login or @DisplayName) — the identifiers known from chat — "
    + "to a single exact user. Known users are matched first; the platform API is only a fallback. "
    + "Outputs Login / DisplayName / UserId / IsFound for later steps.")]
public sealed record LookupPlatformUserAction : WorkflowAction
{
    public const string ActionType = "lookupTwitchUser";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam(
        "Target User",
        "string",
        required: true,
        help: "Login or display name (chat-known), optionally @-prefixed, or a variable such as {Trigger.UserDisplayName}. A numeric user id is not required.")]
    public string? Target { get; init; }

    // Legacy fields (pre-Target). No longer surfaced in the editor, but still deserialized so rules
    // saved before the single-Target redesign keep working. The executor prefers Target.
    public string? Login { get; init; }

    public string? UserId { get; init; }
}
