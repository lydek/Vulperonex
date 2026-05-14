using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Time;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Time;

namespace Vulperonex.Infrastructure.EventBus;

public sealed class ActionExecutionLogStore(
    VulperonexDbContext context,
    IClock? clock = null,
    int maxRetries = 3)
{
    private static readonly TimeSpan StalePendingThreshold = TimeSpan.FromSeconds(30);
    private readonly IClock _clock = clock ?? new SystemClock();

    public async Task<ActionExecutionDecision> BeginExecutionAsync(
        string dedupKey,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(dedupKey, cancellationToken);
        if (existing is null)
        {
            await InsertPendingAsync(dedupKey, cancellationToken);
            return new ActionExecutionDecision(dedupKey, ActionExecutionStatus.Pending, 0, ShouldExecute: true);
        }

        if (existing.Status is ActionExecutionStatus.Completed or ActionExecutionStatus.Failed)
        {
            return new ActionExecutionDecision(existing.DedupKey, existing.Status, existing.AttemptCount, ShouldExecute: false);
        }

        if (_clock.UtcNow - existing.UpdatedAt <= StalePendingThreshold)
        {
            return new ActionExecutionDecision(existing.DedupKey, existing.Status, existing.AttemptCount, ShouldExecute: false);
        }

        if (existing.AttemptCount >= maxRetries)
        {
            existing.Status = ActionExecutionStatus.Failed;
            existing.UpdatedAt = _clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            return new ActionExecutionDecision(existing.DedupKey, existing.Status, existing.AttemptCount, ShouldExecute: false);
        }

        existing.AttemptCount++;
        existing.UpdatedAt = _clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return new ActionExecutionDecision(existing.DedupKey, existing.Status, existing.AttemptCount, ShouldExecute: true);
    }

    public async Task InsertPendingAsync(string dedupKey, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        context.ActionExecutionLogs.Add(new ActionExecutionLogEntity
        {
            DedupKey = dedupKey,
            Status = ActionExecutionStatus.Pending,
            AttemptCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<ActionExecutionLogEntity?> FindAsync(string dedupKey, CancellationToken cancellationToken = default)
    {
        return context.ActionExecutionLogs.FindAsync([dedupKey], cancellationToken).AsTask();
    }

    public Task MarkCompletedAsync(string dedupKey, CancellationToken cancellationToken = default)
    {
        return UpdateStatusAsync(dedupKey, ActionExecutionStatus.Completed, cancellationToken);
    }

    public Task MarkFailedAsync(string dedupKey, CancellationToken cancellationToken = default)
    {
        return UpdateStatusAsync(dedupKey, ActionExecutionStatus.Failed, cancellationToken);
    }

    private async Task UpdateStatusAsync(string dedupKey, string status, CancellationToken cancellationToken)
    {
        var log = await FindAsync(dedupKey, cancellationToken)
            ?? throw new InvalidOperationException($"Action execution log not found: {dedupKey}");

        log.Status = status;
        log.UpdatedAt = _clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
