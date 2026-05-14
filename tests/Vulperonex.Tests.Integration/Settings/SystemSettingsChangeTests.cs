using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Cache;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.Settings;
using Xunit;

namespace Vulperonex.Tests.Integration.Settings;

public sealed class SystemSettingsChangeTests
{
    [Fact]
    public async Task Given_Subscriber_When_SetAsyncRuns_Then_SettingChangedEventIsReceived()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);
        var observer = new RecordingObserver();
        service.Changes.Subscribe(observer);

        await service.SetAsync(SystemSettingKey.BusChannelCapacity, 123, "runtime", TestContext.Current.CancellationToken);

        observer.Events.Should().ContainSingle()
            .Which.Key.Should().Be(SystemSettingKey.BusChannelCapacity);
    }

    [Fact]
    public async Task Given_SubscriberThrows_When_SetAsyncRuns_Then_OtherSubscribersStillReceiveEvent()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);
        service.Changes.Subscribe(new ThrowingObserver());
        var observer = new RecordingObserver();
        service.Changes.Subscribe(observer);

        var act = async () => await service.SetAsync(SystemSettingKey.BusChannelCapacity, 123, "runtime", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        observer.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_BusCapacitySetting_When_BusIsCreated_Then_ConfiguredCapacityIsUsed()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);
        await service.SetAsync(SystemSettingKey.BusChannelCapacity, 64, "runtime", TestContext.Current.CancellationToken);

        await using var bus = await InMemoryStreamEventBus.CreateAsync(service, TestContext.Current.CancellationToken);

        bus.Capacity.Should().Be(64);
    }

    [Fact]
    public async Task Given_DisplayCacheSettings_When_CacheIsCreated_Then_ConfiguredDefaultsAreUsed()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);
        await service.SetAsync(SystemSettingKey.OverlayDisplayCacheL1Capacity, 12, "runtime", TestContext.Current.CancellationToken);
        await service.SetAsync(SystemSettingKey.OverlayDisplayCacheTtlHours, 2, "runtime", TestContext.Current.CancellationToken);

        var cache = await PlatformUserDisplayCache.CreateAsync(context, service, TestContext.Current.CancellationToken);

        cache.L1Capacity.Should().Be(12);
        cache.Ttl.Should().Be(TimeSpan.FromHours(2));
    }

    private sealed class RecordingObserver : IObserver<SettingChangedEvent>
    {
        public List<SettingChangedEvent> Events { get; } = [];
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(SettingChangedEvent value) => Events.Add(value);
    }

    private sealed class ThrowingObserver : IObserver<SettingChangedEvent>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(SettingChangedEvent value) => throw new InvalidOperationException("Subscriber failed.");
    }
}
