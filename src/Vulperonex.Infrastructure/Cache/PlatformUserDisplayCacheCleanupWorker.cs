using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Cache;

public sealed class PlatformUserDisplayCacheCleanupWorker(
    VulperonexDbContext context,
    TimeSpan? ttl = null)
{
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromHours(24);

    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - _ttl;
        return CleanupExpiredAsync(cutoff, cancellationToken);
    }

    private async Task<int> CleanupExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var allRows = await context.PlatformUserDisplayInfo.ToListAsync(cancellationToken);
        var expiredRows = allRows.Where(row => row.FetchedAt < cutoff).ToArray();

        context.PlatformUserDisplayInfo.RemoveRange(expiredRows);
        await context.SaveChangesAsync(cancellationToken);
        return expiredRows.Length;
    }
}
