namespace Vulperonex.Application.Workflows.Conditions;

using System.Text.Json.Serialization;

public sealed record CooldownCondition : WorkflowCondition
{
    public const string ConditionType = "cooldown";

    [JsonIgnore]
    public override string Type => ConditionType;
    public CooldownScope Scope { get; init; } = CooldownScope.Global;
    public int DurationSeconds { get; init; }
    public string Key { get; init; } = string.Empty;
}
