using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Logging;

/// <summary>
/// Periodically prunes the AppLogs table by retention age and / or rolling
/// SQLite file size, whichever triggers first (Task 18d). After a size-based
/// trim a VACUUM is issued to release pages back to the OS.
/// </summary>
public sealed class AppLogsCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AppLogsCleanupWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    public const int DefaultRetentionDays = 30;
    public const int DefaultMaxSizeMb = 50;
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AppLogs cleanup sweep failed; will retry on next interval.");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task ExecuteOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        var retentionDays = await settings.GetAsync(SystemSettingKey.LogDbRetentionDays, DefaultRetentionDays, cancellationToken).ConfigureAwait(false);
        var maxSizeMb = await settings.GetAsync(SystemSettingKey.LogDbMaxSizeMb, DefaultMaxSizeMb, cancellationToken).ConfigureAwait(false);

        var retentionDeleted = await DeleteByRetentionAsync(context, retentionDays, cancellationToken).ConfigureAwait(false);
        var sizeDeleted = await TrimBySizeAsync(context, maxSizeMb, cancellationToken).ConfigureAwait(false);

        if (sizeDeleted > 0)
        {
            await context.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken).ConfigureAwait(false);
        }

        if (retentionDeleted > 0 || sizeDeleted > 0)
        {
            logger.LogInformation(
                "AppLogs cleanup pruned {RetentionDeleted} retention rows and {SizeDeleted} size-trimmed rows.",
                retentionDeleted,
                sizeDeleted);
        }
    }

    private async Task<int> DeleteByRetentionAsync(VulperonexDbContext context, int retentionDays, CancellationToken cancellationToken)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        // SQLite stores DateTimeOffset as ISO 8601 TEXT which is
        // lexically sortable. Raw SQL avoids EF's translation gap for
        // DateTimeOffset comparisons against the column type.
        var cutoff = timeProvider.GetUtcNow()
            .AddDays(-retentionDays)
            .ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        return await context.Database
            .ExecuteSqlRawAsync("DELETE FROM AppLogs WHERE CreatedAt < {0};", new object[] { cutoff }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> TrimBySizeAsync(VulperonexDbContext context, int maxSizeMb, CancellationToken cancellationToken)
    {
        if (maxSizeMb <= 0)
        {
            return 0;
        }

        var maxBytes = (long)maxSizeMb * 1024L * 1024L;
        var totalDeleted = 0;

        for (var safetyIteration = 0; safetyIteration < 10; safetyIteration++)
        {
            var currentBytes = await GetDbSizeBytesAsync(context, cancellationToken).ConfigureAwait(false);
            if (currentBytes <= maxBytes)
            {
                break;
            }

            // Drop the oldest 10% of rows, capped at 1000 per pass to avoid
            // single huge DELETE blocking other writers.
            var totalRows = await context.AppLogs.CountAsync(cancellationToken).ConfigureAwait(false);
            if (totalRows == 0)
            {
                break;
            }

            var trimCount = Math.Min(1_000, Math.Max(100, totalRows / 10));
            // Order by Id rather than CreatedAt because EF SQLite cannot
            // translate ORDER BY on the DateTimeOffset column; Id is an
            // auto-increment integer so smallest Id == oldest row in
            // practice for an append-only log table.
            var ids = await context.AppLogs
                .OrderBy(log => log.Id)
                .Select(log => log.Id)
                .Take(trimCount)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            if (ids.Length == 0)
            {
                break;
            }

            var deleted = await context.AppLogs
                .Where(log => ids.Contains(log.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            totalDeleted += deleted;
            if (deleted == 0)
            {
                break;
            }
        }

        return totalDeleted;
    }

    private static async Task<long> GetDbSizeBytesAsync(VulperonexDbContext context, CancellationToken cancellationToken)
    {
        // EF owns the connection lifetime; do not dispose it here.
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var pageCountCommand = connection.CreateCommand();
        pageCountCommand.CommandText = "PRAGMA page_count;";
        var pageCount = Convert.ToInt64(await pageCountCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

        await using var pageSizeCommand = connection.CreateCommand();
        pageSizeCommand.CommandText = "PRAGMA page_size;";
        var pageSize = Convert.ToInt64(await pageSizeCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

        return pageCount * pageSize;
    }
}
