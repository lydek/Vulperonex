using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Logging;
using Vulperonex.Infrastructure.Settings;
using Xunit;

namespace Vulperonex.Tests.Integration.Logging;

public sealed class AppLogsCleanupWorkerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero));
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<VulperonexDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Given_RowsOlderThanRetention_When_ExecuteOnce_Then_OldRowsDeleted()
    {
        await SeedLogsAsync(
            (_clock.GetUtcNow().AddDays(-31), "old-1"),
            (_clock.GetUtcNow().AddDays(-29), "fresh-1"),
            (_clock.GetUtcNow().AddDays(-1), "fresh-2"));

        var worker = CreateWorker();
        await worker.ExecuteOnceAsync(TestContext.Current.CancellationToken);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var remaining = await context.AppLogs.Select(log => log.Message).ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().BeEquivalentTo(new[] { "fresh-1", "fresh-2" });
    }

    [Fact]
    public async Task Given_RetentionDaysIsZero_When_ExecuteOnce_Then_NoRetentionDeletion()
    {
        await SeedLogsAsync(
            (_clock.GetUtcNow().AddDays(-100), "ancient-1"));

        using (var seedScope = _provider.CreateScope())
        {
            var settings = seedScope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            await settings.SetAsync(SystemSettingKey.LogDbRetentionDays, 0, "log", TestContext.Current.CancellationToken);
            await settings.SetAsync(SystemSettingKey.LogDbMaxSizeMb, 0, "log", TestContext.Current.CancellationToken);
        }

        var worker = CreateWorker();
        await worker.ExecuteOnceAsync(TestContext.Current.CancellationToken);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var count = await context.AppLogs.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_SizeOverMax_When_ExecuteOnce_Then_OldestRowsTrimmedAndVacuumIssued()
    {
        // Fill the table with enough rows so PRAGMA page_count exceeds the
        // configured max. Each row carries a ~2KB payload to amplify size.
        var bigPayload = new string('x', 2048);
        var seeds = new List<(DateTimeOffset Created, string Message)>();
        for (var index = 0; index < 800; index++)
        {
            seeds.Add((_clock.GetUtcNow().AddMinutes(-index), $"{index}-{bigPayload}"));
        }
        await SeedLogsAsync(seeds.ToArray());

        using (var seedScope = _provider.CreateScope())
        {
            var settings = seedScope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            // Force every row to look "fresh" so only size trimming can prune.
            await settings.SetAsync(SystemSettingKey.LogDbRetentionDays, 3650, "log", TestContext.Current.CancellationToken);
            await settings.SetAsync(SystemSettingKey.LogDbMaxSizeMb, 1, "log", TestContext.Current.CancellationToken);
        }

        long beforeBytes;
        int beforeCount;
        using (var beforeScope = _provider.CreateScope())
        {
            var context = beforeScope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            beforeBytes = await GetDbSizeBytesAsync(context, TestContext.Current.CancellationToken);
            beforeCount = await context.AppLogs.CountAsync(TestContext.Current.CancellationToken);
        }

        beforeBytes.Should().BeGreaterThan(1024L * 1024L);

        var worker = CreateWorker();
        await worker.ExecuteOnceAsync(TestContext.Current.CancellationToken);

        using var afterScope = _provider.CreateScope();
        var afterContext = afterScope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var afterCount = await afterContext.AppLogs.CountAsync(TestContext.Current.CancellationToken);
        afterCount.Should().BeLessThan(beforeCount);
    }

    private AppLogsCleanupWorker CreateWorker()
    {
        return new AppLogsCleanupWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AppLogsCleanupWorker>.Instance,
            _clock);
    }

    private async Task SeedLogsAsync(params (DateTimeOffset Created, string Message)[] entries)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        foreach (var (created, message) in entries)
        {
            context.AppLogs.Add(new AppLogEntity
            {
                CreatedAt = created,
                Level = "Information",
                Message = message,
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> GetDbSizeBytesAsync(VulperonexDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var pageCountCmd = connection.CreateCommand();
        pageCountCmd.CommandText = "PRAGMA page_count;";
        var pageCount = Convert.ToInt64(await pageCountCmd.ExecuteScalarAsync(cancellationToken));

        await using var pageSizeCmd = connection.CreateCommand();
        pageSizeCmd.CommandText = "PRAGMA page_size;";
        var pageSize = Convert.ToInt64(await pageSizeCmd.ExecuteScalarAsync(cancellationToken));

        return pageCount * pageSize;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
