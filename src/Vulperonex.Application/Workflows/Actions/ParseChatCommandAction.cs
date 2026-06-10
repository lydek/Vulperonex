using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata(
    "Parse Chat Command",
    "Extract command arguments from chat text for later user lookup or shoutout steps.")]
public sealed record ParseChatCommandAction : WorkflowAction
{
    public const string ActionType = "parseChatCommand";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Message", "string", required: false, help: "Chat message text. Empty = Trigger.MessageText.")]
    public string? Message { get; init; }

    [ActionParam("Command Prefix", "string", required: false, help: "Optional command prefix to strip, e.g. !shoutout.")]
    public string? CommandPrefix { get; init; }
}
