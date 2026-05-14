using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows;

public interface IWorkflowRuleInvoker
{
    Task InvokeAsync(
        string workflowRuleId,
        IStreamEvent streamEvent,
        string invocationId,
        CancellationToken cancellationToken = default);
}
