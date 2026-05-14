using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;

namespace Vulperonex.Application.Workflows;

public sealed record WorkflowRule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string EventTypeKey { get; init; }
    public IReadOnlyList<WorkflowCondition> Conditions { get; init; } = [];
    public IReadOnlyList<WorkflowAction> Actions { get; init; } = [];
    public bool IsEnabled { get; init; } = true;
    public int Priority { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public WorkflowExecutionMode ExecutionMode { get; init; } = WorkflowExecutionMode.Serial;
    public int MaxParallelism { get; init; } = 1;
}
