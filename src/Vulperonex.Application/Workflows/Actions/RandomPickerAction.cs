using System.Text.Json.Serialization;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record RandomPickerAction : WorkflowAction
{
    public const string ActionType = "randomPicker";

    [JsonIgnore]
    public override string Type => ActionType;

    public IReadOnlyList<string> Choices { get; init; } = [];

    public IReadOnlyList<int>? Weights { get; init; }
}
