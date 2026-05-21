using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Overlay;
using Xunit;

namespace Vulperonex.Tests.Integration.Overlay;

public sealed class OverlayHistoryServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<VulperonexDbContext>(options => options.UseSqlite(_connection));
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
    public async Task Given_NewService_When_AddAsync_Then_PersistsSnapshotToSystemSettings()
    {
        var service = CreateService(capacity: 5);
        var payload = NewAlert("evt-1", "First");

        await service.AddAsync(payload, TestContext.Current.CancellationToken);

        var stored = await ReadDataRowAsync();
        stored.Should().NotBeNull();
        stored!.Value.Should().Contain("evt-1");
        stored.Category.Should().Be("overlay.history");
    }

    [Fact]
    public async Task Given_AddExceedsCapacity_When_AddAsync_Then_OldestEntryIsDropped()
    {
        var service = CreateService(capacity: 2);

        await service.AddAsync(NewAlert("evt-1", "First"), TestContext.Current.CancellationToken);
        await service.AddAsync(NewAlert("evt-2", "Second"), TestContext.Current.CancellationToken);
        await service.AddAsync(NewAlert("evt-3", "Third"), TestContext.Current.CancellationToken);

        var snapshot = await service.GetRecentAsync(TestContext.Current.CancellationToken);
        snapshot.Select(item => item.EventId).Should().Equal("evt-2", "evt-3");
    }

    [Fact]
    public async Task Given_ExistingSnapshot_When_NewServiceLoads_Then_CacheIsRehydrated()
    {
        var first = CreateService(capacity: 5);
        await first.AddAsync(NewAlert("evt-1", "First"), TestContext.Current.CancellationToken);
        await first.AddAsync(NewAlert("evt-2", "Second"), TestContext.Current.CancellationToken);

        var second = CreateService(capacity: 5);
        var snapshot = await second.GetRecentAsync(TestContext.Current.CancellationToken);

        snapshot.Select(item => item.EventId).Should().Equal("evt-1", "evt-2");
    }

    [Fact]
    public async Task Given_ClearAllAsync_Then_CacheAndRowAreRemoved()
    {
        var service = CreateService(capacity: 5);
        await service.AddAsync(NewAlert("evt-1", "First"), TestContext.Current.CancellationToken);

        await service.ClearAllAsync(TestContext.Current.CancellationToken);

        (await service.GetRecentAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await ReadDataRowAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Given_CapacityOverrideInSystemSettings_When_ServiceLoads_Then_OverrideIsApplied()
    {
        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            context.SystemSettings.Add(new SystemSettingEntity
            {
                Key = "overlay.history.cap.alerts",
                Value = "1",
                Category = "overlay.history",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var service = CreateService(capacity: 10);

        await service.AddAsync(NewAlert("evt-1", "First"), TestContext.Current.CancellationToken);
        await service.AddAsync(NewAlert("evt-2", "Second"), TestContext.Current.CancellationToken);

        service.Capacity.Should().Be(1);
        var snapshot = await service.GetRecentAsync(TestContext.Current.CancellationToken);
        snapshot.Select(item => item.EventId).Should().Equal("evt-2");
    }

    [Fact]
    public async Task Given_MissingRow_When_GetRecentAsync_Then_ReturnsEmptyAndDoesNotThrow()
    {
        var service = CreateService(capacity: 5);

        var snapshot = await service.GetRecentAsync(TestContext.Current.CancellationToken);

        snapshot.Should().BeEmpty();
    }

    private OverlayHistoryService<OverlayAlertPayload> CreateService(int capacity)
    {
        var options = new OverlayHistoryOptions<OverlayAlertPayload>
        {
            HubName = "alerts",
            DefaultCapacity = capacity,
        };
        return new OverlayHistoryService<OverlayAlertPayload>(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OverlayHistoryService<OverlayAlertPayload>>.Instance,
            options);
    }

    private async Task<SystemSettingEntity?> ReadDataRowAsync()
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        return await context.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Key == "overlay.history.alerts", TestContext.Current.CancellationToken);
    }

    private static OverlayAlertPayload NewAlert(string eventId, string displayName) =>
        new(1, eventId, DateTimeOffset.UtcNow, displayName, "followed", null);
}
