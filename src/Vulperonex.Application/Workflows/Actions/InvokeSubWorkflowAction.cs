using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

namespace Vulperonex.Application.Workflows.Actions;

[ActionMetadata("Invoke Sub-Workflow", "Invoke another sub-workflow rule")]
public sealed record InvokeSubWorkflowAction : WorkflowAction
{
    public const string ActionType = "invokeSubWorkflow";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("Workflow ID", "string", required: true, help: "Target workflow ID to execute")]
    public required string WorkflowId { get; init; }

    [ActionParam("Arguments", "dictionary", required: false, help: "Key-value template arguments passed into the sub-workflow")]
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
