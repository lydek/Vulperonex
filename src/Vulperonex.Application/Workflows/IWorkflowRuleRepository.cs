namespace Vulperonex.Application.Workflows;

public interface IWorkflowRuleRepository
{
    Task AddAsync(WorkflowRule rule, CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkflowRule rule, int expectedVersion, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string id, bool isEnabled, int expectedVersion, CancellationToken cancellationToken = default);
}
