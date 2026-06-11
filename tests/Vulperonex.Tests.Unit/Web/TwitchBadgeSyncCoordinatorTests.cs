using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vulperonex.Application.Twitch;
using Vulperonex.Adapters.Twitch.Helix;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class TwitchBadgeSyncCoordinatorTests
{
    [Fact]
    public async Task SyncAsync_WhenBroadcasterIdConfigured_SyncsGlobalAndChannelBadges()
    {
        var cache = Substitute.For<IPlatformBadgeCache>();
        var coordinator = CreateCoordinator(cache, broadcasterId: "broadcaster-1");

        await coordinator.SyncAsync(TestContext.Current.CancellationToken);

        await cache.Received(1).SyncGlobalAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).SyncChannelAsync("broadcaster-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenBroadcasterIdMissing_SyncsGlobalOnly()
    {
        var cache = Substitute.For<IPlatformBadgeCache>();
        var coordinator = CreateCoordinator(cache, broadcasterId: null);

        await coordinator.SyncAsync(TestContext.Current.CancellationToken);

        await cache.Received(1).SyncGlobalAsync(Arg.Any<CancellationToken>());
        await cache.DidNotReceive().SyncChannelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueSync_WhenCalledRepeatedly_DoesNotRunOverlappingSyncs()
    {
        var cache = Substitute.For<IPlatformBadgeCache>();
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var globalSyncCalls = 0;
        cache.SyncGlobalAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            Interlocked.Increment(ref globalSyncCalls);
            syncStarted.TrySetResult();
            await releaseSync.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        var coordinator = CreateCoordinator(cache, broadcasterId: null);

        coordinator.QueueSync();
        await syncStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        coordinator.QueueSync();
        coordinator.QueueSync();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        globalSyncCalls.Should().Be(1);
        releaseSync.SetResult();
        await EventuallyAsync(() => Volatile.Read(ref globalSyncCalls).Should().Be(1));
    }

    [Fact]
    public async Task QueueSync_WhenDirectSyncIsRunning_WaitsForTheActiveSync()
    {
        var cache = Substitute.For<IPlatformBadgeCache>();
        var firstSyncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var globalSyncCalls = 0;
        cache.SyncGlobalAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (Interlocked.Increment(ref globalSyncCalls) == 1)
            {
                firstSyncStarted.TrySetResult();
                await releaseFirstSync.Task.WaitAsync(TestContext.Current.CancellationToken);
            }
        });
        var coordinator = CreateCoordinator(cache, broadcasterId: null);

        var directSync = coordinator.SyncAsync(TestContext.Current.CancellationToken);
        await firstSyncStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        coordinator.QueueSync();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        globalSyncCalls.Should().Be(1);

        releaseFirstSync.SetResult();
        await directSync;
        await EventuallyAsync(() => Volatile.Read(ref globalSyncCalls).Should().Be(2));
    }

    private static TwitchBadgeSyncCoordinator CreateCoordinator(IPlatformBadgeCache cache, string? broadcasterId)
    {
        var values = new Dictionary<string, string?>();
        if (broadcasterId is not null)
        {
            values["Twitch:BroadcasterId"] = broadcasterId;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new TwitchBadgeSyncCoordinator(
            cache,
            configuration,
            Substitute.For<IHostApplicationLifetime>(),
            NullLogger<TwitchBadgeSyncCoordinator>.Instance);
    }

    private static async Task EventuallyAsync(Action assertion)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }
        }

        throw last ?? new TimeoutException("Assertion did not pass.");
    }
}
