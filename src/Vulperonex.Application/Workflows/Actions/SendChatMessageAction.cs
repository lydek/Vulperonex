namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record SendChatMessageAction : WorkflowAction
{
    public const string ActionType = "sendChatMessage";

    [JsonIgnore]
    public override string Type => ActionType;
    public required string Template { get; init; }
    public string? TargetPlatform { get; init; }
    public string? Channel { get; init; }
    public string? DedupKey { get; init; }
}
