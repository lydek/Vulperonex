namespace Vulperonex.Application.Workflows;

public sealed record ActionExecutionKey(
    string EventId,
    string WorkflowRuleId,
    int ActionIndex,
    string? InvocationId = null,
    WorkflowExecutionPhase Phase = WorkflowExecutionPhase.Main);
