using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record ActionExecutionContext(
    IStreamEvent StreamEvent,
    WorkflowRule WorkflowRule,
    int ActionIndex,
    string? InvocationId = null);
