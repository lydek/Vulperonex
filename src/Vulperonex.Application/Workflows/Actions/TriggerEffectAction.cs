namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record TriggerEffectAction : WorkflowAction
{
    public const string ActionType = "triggerEffect";

    [JsonIgnore]
    public override string Type => ActionType;

    public required string EffectId { get; init; }

    public int? DurationMs { get; init; }
}
