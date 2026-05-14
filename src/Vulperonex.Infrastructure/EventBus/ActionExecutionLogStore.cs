using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.EventBus;

public sealed class ActionExecutionLogStore(VulperonexDbContext context)
{
    public async Task InsertPendingAsync(string dedupKey, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
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
        log.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
