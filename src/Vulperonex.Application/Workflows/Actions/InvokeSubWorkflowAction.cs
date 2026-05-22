namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record InvokeSubWorkflowAction : WorkflowAction
{
    public const string ActionType = "invokeSubWorkflow";

    [JsonIgnore]
    public override string Type => ActionType;
    public required string WorkflowId { get; init; }
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
