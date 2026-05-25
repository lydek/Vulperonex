using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Logging;

public sealed class MemberAuditLogsPruningWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MemberAuditLogsPruningWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    public const int DefaultRetentionDays = 365;
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
                logger.LogWarning(ex, "MemberAuditLogs cleanup sweep failed; will retry on next interval.");
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

        var retentionDays = await settings.GetAsync(SystemSettingKey.MembersAuditRetentionDays, DefaultRetentionDays, cancellationToken).ConfigureAwait(false);

        if (retentionDays <= 0)
        {
            return;
        }

        var cutoff = timeProvider.GetUtcNow()
            .AddDays(-retentionDays)
            .ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        var prunedCount = await context.Database
            .ExecuteSqlRawAsync("DELETE FROM MemberAuditLogs WHERE OccurredAt < {0};", [cutoff], cancellationToken)
            .ConfigureAwait(false);

        if (prunedCount > 0)
        {
            logger.LogInformation("MemberAuditLogs cleanup pruned {PrunedCount} retention rows.", prunedCount);
        }
    }
}
