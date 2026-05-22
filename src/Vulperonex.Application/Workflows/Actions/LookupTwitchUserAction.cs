namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record LookupTwitchUserAction : WorkflowAction
{
    public const string ActionType = "lookupTwitchUser";

    [JsonIgnore]
    public override string Type => ActionType;

    public string? Login { get; init; }

    public string? UserId { get; init; }
}
