using System;
using System.Linq;
using System.Threading.Tasks;
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

public sealed class MemberAuditLogsPruningWorkerTests : IAsyncLifetime
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
        var memberId = "sim-prune-member";
        await SeedMemberAsync(memberId);

        await SeedAuditLogsAsync(
            (memberId, "log-1", _clock.GetUtcNow().AddDays(-366), "old log"),
            (memberId, "log-2", _clock.GetUtcNow().AddDays(-364), "fresh log 1"),
            (memberId, "log-3", _clock.GetUtcNow().AddDays(-1), "fresh log 2")
        );

        var worker = CreateWorker();
        await worker.ExecuteOnceAsync(TestContext.Current.CancellationToken);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var remaining = await context.MemberAuditLogs.Select(log => log.Reason).ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().BeEquivalentTo(new[] { "fresh log 1", "fresh log 2" });
    }

    [Fact]
    public async Task Given_RetentionDaysIsZero_When_ExecuteOnce_Then_NoPruning()
    {
        var memberId = "sim-prune-member-zero";
        await SeedMemberAsync(memberId);

        await SeedAuditLogsAsync(
            (memberId, "log-zero", _clock.GetUtcNow().AddDays(-400), "ancient log")
        );

        using (var seedScope = _provider.CreateScope())
        {
            var settings = seedScope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            await settings.SetAsync(SystemSettingKey.MembersAuditRetentionDays, 0, "log", TestContext.Current.CancellationToken);
        }

        var worker = CreateWorker();
        await worker.ExecuteOnceAsync(TestContext.Current.CancellationToken);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var count = await context.MemberAuditLogs.CountAsync(TestContext.Current.CancellationToken);
        count.Should().Be(1);
    }

    private MemberAuditLogsPruningWorker CreateWorker()
    {
        return new MemberAuditLogsPruningWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemberAuditLogsPruningWorker>.Instance,
            _clock);
    }

    private async Task SeedMemberAsync(string memberId)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        context.Members.Add(new MemberEntity
        {
            MemberId = memberId,
            CheckInCount = 1,
            TotalLoyalty = 10
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedAuditLogsAsync(params (string MemberId, string Id, DateTimeOffset Occurred, string Reason)[] entries)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        foreach (var (memberId, id, occurred, reason) in entries)
        {
            context.MemberAuditLogs.Add(new MemberAuditLogEntity
            {
                Id = id,
                MemberId = memberId,
                OccurredAt = occurred,
                ActorKind = "system",
                Operation = "adjust_loyalty",
                Reason = reason
            });
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
