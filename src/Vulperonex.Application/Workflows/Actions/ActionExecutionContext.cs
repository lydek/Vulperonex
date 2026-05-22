using Vulperonex.Application.Expressions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed record ActionExecutionContext
{
    public ActionExecutionContext(
        IStreamEvent StreamEvent,
        WorkflowRule WorkflowRule,
        int ActionIndex,
        string? InvocationId = null,
        ExpressionContext? ExpressionContext = null,
        WorkflowExecutionPhase Phase = WorkflowExecutionPhase.Main)
    {
        this.StreamEvent = StreamEvent;
        this.WorkflowRule = WorkflowRule;
        this.ActionIndex = ActionIndex;
        this.InvocationId = InvocationId;
        this.ExpressionContext = ExpressionContext ?? global::Vulperonex.Application.Expressions.ExpressionContext.Empty;
        this.Phase = Phase;
    }

    public IStreamEvent StreamEvent { get; init; }
    public WorkflowRule WorkflowRule { get; init; }
    public int ActionIndex { get; init; }
    public string? InvocationId { get; init; }
    public ExpressionContext ExpressionContext { get; init; }
    public WorkflowExecutionPhase Phase { get; init; }
}
