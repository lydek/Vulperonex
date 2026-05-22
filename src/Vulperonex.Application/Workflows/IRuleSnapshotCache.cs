namespace Vulperonex.Application.Workflows;

public interface IRuleSnapshotCache
{
    Task<IReadOnlyList<WorkflowRule>> GetByEventTypeAsync(
        string eventTypeKey,
        CancellationToken cancellationToken = default);

    Task<WorkflowRule?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    void Invalidate(string? ruleId = null);
}
