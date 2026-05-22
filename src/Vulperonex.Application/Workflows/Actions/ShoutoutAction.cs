namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record ShoutoutAction : WorkflowAction
{
    public const string ActionType = "shoutout";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string TargetLogin { get; init; }
}
