namespace Vulperonex.Application.Workflows.Timers;

public interface IWorkflowTimerRepository
{
    Task<IReadOnlyList<WorkflowTimer>> ListAsync(CancellationToken cancellationToken = default);

    Task<WorkflowTimer?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(WorkflowTimer timer, CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkflowTimer timer, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTimer>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task MarkFiredAsync(string id, DateTimeOffset nextFireAt, CancellationToken cancellationToken = default);
}
