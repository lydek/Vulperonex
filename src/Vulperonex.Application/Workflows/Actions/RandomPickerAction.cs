using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Random Picker", "Pick a random choice from a list of options")]
public sealed record RandomPickerAction : WorkflowAction
{
    public const string ActionType = "randomPicker";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Choices", "array", required: true, help: "List of string choices to pick from")]
    public IReadOnlyList<string> Choices { get; init; } = [];

    [ActionParam("Weights", "array", required: false, help: "Relative probability weights for each choice")]
    public IReadOnlyList<int>? Weights { get; init; }
}
