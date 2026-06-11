using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Workflows;

public sealed class WorkflowTimerRepository(VulperonexDbContext context) : IWorkflowTimerRepository
{
    public async Task<IReadOnlyList<WorkflowTimer>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowTimers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .OrderBy(timer => timer.NextFireAt)
            .ThenBy(timer => timer.Id)
            .Select(ToDomain)
            .ToArray();
    }

    public async Task<WorkflowTimer?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowTimers
            .AsNoTracking()
            .FirstOrDefaultAsync(timer => timer.Id == id, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(WorkflowTimer timer, CancellationToken cancellationToken = default)
    {
        Validate(timer);
        context.WorkflowTimers.Add(ToEntity(timer));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowTimer timer, int expectedVersion, CancellationToken cancellationToken = default)
    {
        Validate(timer);
        var entity = await context.WorkflowTimers
            .FirstOrDefaultAsync(existing => existing.Id == timer.Id && existing.Version == expectedVersion, cancellationToken);

        if (entity is null)
        {
            var exists = await context.WorkflowTimers.AnyAsync(existing => existing.Id == timer.Id, cancellationToken);
            throw exists
                ? new WorkflowTimerConcurrencyException(timer.Id)
                : new KeyNotFoundException(timer.Id);
        }

        CopyToEntity(timer, entity);
        entity.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WorkflowTimerConcurrencyException(timer.Id);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowTimers.FirstOrDefaultAsync(timer => timer.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(id);

        context.WorkflowTimers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowTimer>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowTimers
            .AsNoTracking()
            .Where(timer => timer.IsEnabled)
            .ToListAsync(cancellationToken);

        return entities
            .Where(timer => timer.NextFireAt <= now)
            .OrderBy(timer => timer.NextFireAt)
            .ThenBy(timer => timer.Id)
            .Select(ToDomain)
            .ToArray();
    }

    public async Task MarkFiredAsync(string id, DateTimeOffset nextFireAt, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowTimers.FirstOrDefaultAsync(timer => timer.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(id);

        entity.NextFireAt = nextFireAt;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent admin edit bumped the version; its NextFireAt wins
            // over the scheduler's reschedule, so dropping this write is safe.
        }
    }

    private static WorkflowTimer ToDomain(WorkflowTimerEntity entity)
    {
        return new WorkflowTimer
        {
            Id = entity.Id,
            RuleId = entity.RuleId,
            IntervalSeconds = entity.IntervalSeconds,
            IsEnabled = entity.IsEnabled,
            NextFireAt = entity.NextFireAt,
            Version = entity.Version,
        };
    }

    private static WorkflowTimerEntity ToEntity(WorkflowTimer timer)
    {
        var entity = new WorkflowTimerEntity();
        CopyToEntity(timer, entity);
        return entity;
    }

    private static void CopyToEntity(WorkflowTimer timer, WorkflowTimerEntity entity)
    {
        entity.Id = timer.Id;
        entity.RuleId = timer.RuleId;
        entity.IntervalSeconds = timer.IntervalSeconds;
        entity.IsEnabled = timer.IsEnabled;
        entity.NextFireAt = timer.NextFireAt;
    }

    private static void Validate(WorkflowTimer timer)
    {
        if (string.IsNullOrWhiteSpace(timer.Id))
        {
            throw new ArgumentException("Workflow timer id is required.", nameof(timer));
        }

        if (string.IsNullOrWhiteSpace(timer.RuleId))
        {
            throw new ArgumentException("Workflow timer rule id is required.", nameof(timer));
        }

        if (timer.IntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timer), "Workflow timer interval must be positive.");
        }
    }
}
