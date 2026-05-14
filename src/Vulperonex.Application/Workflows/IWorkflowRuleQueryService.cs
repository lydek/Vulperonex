using Vulperonex.Application.Workflows.Dtos;

namespace Vulperonex.Application.Workflows;

public interface IWorkflowRuleQueryService
{
    Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(
        string eventTypeKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowRuleSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<WorkflowRule?> GetAsync(string id, CancellationToken cancellationToken = default);
}
