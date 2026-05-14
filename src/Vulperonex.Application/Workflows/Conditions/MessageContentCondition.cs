namespace Vulperonex.Application.Workflows.Conditions;

using System.Text.Json.Serialization;

public sealed record MessageContentCondition : WorkflowCondition
{
    public const string ConditionType = "messageContent";

    [JsonIgnore]
    public override string Type => ConditionType;
    public MessageContentMatchMode MatchMode { get; init; } = MessageContentMatchMode.ContainsMatch;
    public string Pattern { get; init; } = string.Empty;
    public bool IgnoreCase { get; init; } = true;
}
