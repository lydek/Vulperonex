using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.Cache;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Members;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class MemberEquivalenceTests
{
    private static readonly StreamUser TestUser = new("twitch", "alice", "Alice");

    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchMessage_When_Published_Then_MemberDatabaseStateMatches()
    {
        var (simId, simMem, simCache) = await RunSimulationAsync(async (adapter, ct) =>
        {
            await adapter.SimulateAsync(SimulationRequest.Message("twitch", TestUser, "!hello"), ct);
        });

        var (twId, twMem, twCache) = await RunTwitchAsync(async (adapter, ct) =>
        {
            await adapter.PublishMockPayloadAsync(
                new TwitchMockPayload(TwitchMockPayloadKind.Message, TestUser, MessageText: "!hello"),
                ct);
        });

        twId.Should().BeEquivalentTo(simId);
        twMem.Should().BeEquivalentTo(simMem);
        twCache.Should().BeEquivalentTo(simCache);
    }

    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchFollow_When_Published_Then_MemberDatabaseStateMatches()
    {
        var (simId, simMem, simCache) = await RunSimulationAsync(async (adapter, ct) =>
        {
            await adapter.SimulateAsync(SimulationRequest.Followed("twitch", TestUser), ct);
        });

        var (twId, twMem, twCache) = await RunTwitchAsync(async (adapter, ct) =>
        {
            await adapter.PublishMockPayloadAsync(
                new TwitchMockPayload(TwitchMockPayloadKind.Followed, TestUser),
                ct);
        });

        twId.Should().BeEquivalentTo(simId);
        twMem.Should().BeEquivalentTo(simMem);
        twCache.Should().BeEquivalentTo(simCache);
        
        twId.Should().ContainSingle().Which.IsFollower.Should().BeTrue();
    }

    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchSub_When_Published_Then_MemberDatabaseStateMatches()
    {
        var (simId, simMem, simCache) = await RunSimulationAsync(async (adapter, ct) =>
        {
            await adapter.SimulateAsync(SimulationRequest.Subscribed("twitch", TestUser, "2000"), ct);
        });

        var (twId, twMem, twCache) = await RunTwitchAsync(async (adapter, ct) =>
        {
            await adapter.PublishMockPayloadAsync(
                new TwitchMockPayload(TwitchMockPayloadKind.Subscribed, TestUser, Tier: "2000"),
                ct);
        });

        twId.Should().BeEquivalentTo(simId);
        twMem.Should().BeEquivalentTo(simMem);
        twCache.Should().BeEquivalentTo(simCache);

        var id = twId.Should().ContainSingle().Which;
        id.IsSubscriber.Should().BeTrue();
        id.SubscriptionTier.Should().Be("2000");

        var cache = twCache.Should().ContainSingle().Which;
        cache.IsSubscriber.Should().BeTrue();
        cache.SubscriptionTier.Should().Be("2000");
    }

    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchDonation_When_Published_Then_MemberDatabaseStateMatches()
    {
        var (simId, simMem, simCache) = await RunSimulationAsync(async (adapter, ct) =>
        {
            await adapter.SimulateAsync(SimulationRequest.Donated("twitch", TestUser, 500), ct);
        });

        var (twId, twMem, twCache) = await RunTwitchAsync(async (adapter, ct) =>
        {
            await adapter.PublishMockPayloadAsync(
                new TwitchMockPayload(TwitchMockPayloadKind.Donated, TestUser, TotalBitsGiven: 500),
                ct);
        });

        twId.Should().BeEquivalentTo(simId);
        twMem.Should().BeEquivalentTo(simMem);
        twCache.Should().BeEquivalentTo(simCache);

        var cache = twCache.Should().ContainSingle().Which;
        cache.TotalBitsGiven.Should().Be(500);
    }

    [Fact]
    public async Task Given_MonotonicDonations_When_Published_Then_CacheTotalBitsGivenIsMonotonicAndMatches()
    {
        var (simId, simMem, simCache) = await RunSimulationAsync(async (adapter, ct) =>
        {
            await adapter.SimulateAsync(SimulationRequest.Donated("twitch", TestUser, 100), ct);
            await adapter.SimulateAsync(SimulationRequest.Donated("twitch", TestUser, 50), ct);
            await adapter.SimulateAsync(SimulationRequest.Donated("twitch", TestUser, 150), ct);
        });

        var (twId, twMem, twCache) = await RunTwitchAsync(async (adapter, ct) =>
        {
            await adapter.PublishMockPayloadAsync(new TwitchMockPayload(TwitchMockPayloadKind.Donated, TestUser, TotalBitsGiven: 100), ct);
            await adapter.PublishMockPayloadAsync(new TwitchMockPayload(TwitchMockPayloadKind.Donated, TestUser, TotalBitsGiven: 50), ct);
            await adapter.PublishMockPayloadAsync(new TwitchMockPayload(TwitchMockPayloadKind.Donated, TestUser, TotalBitsGiven: 150), ct);
        });

        twId.Should().BeEquivalentTo(simId);
        twMem.Should().BeEquivalentTo(simMem);
        twCache.Should().BeEquivalentTo(simCache);

        var cache = twCache.Should().ContainSingle().Which;
        cache.TotalBitsGiven.Should().Be(150); // Should be 150, demonstrating monotonic behavior (not dropping to 50, and updating to 150)
    }

    private static async Task<(MemberIdentitySnapshot[] Identities, MemberSnapshot[] Members, DisplayInfoSnapshot[] Cache)> RunSimulationAsync(
        Func<SimulationAdapter, CancellationToken, Task> trigger)
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(bus, new MemberResolver(context), new MemberStreamStateRepository(context));
        module.Start();

        var displayCache = new PlatformUserDisplayCache(context);
        var displayCacheUpdater = new TwitchDisplayCacheUpdater(displayCache);
        
        // Subscribe displayCacheUpdater to simulation bus so we also build the equivalent cache state for comparison
        bus.Subscribe<IStreamEvent>((streamEvent, cancellationToken) =>
        {
            return displayCacheUpdater.ApplyAsync(streamEvent, cancellationToken);
        });

        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());
        await adapter.StartAsync(TestContext.Current.CancellationToken);

        await trigger(adapter, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        return (
            await SnapshotIdentitiesAsync(context),
            await SnapshotMembersAsync(context),
            await SnapshotCacheAsync(context)
        );
    }

    private static async Task<(MemberIdentitySnapshot[] Identities, MemberSnapshot[] Members, DisplayInfoSnapshot[] Cache)> RunTwitchAsync(
        Func<TwitchAdapter, CancellationToken, Task> trigger)
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(bus, new MemberResolver(context), new MemberStreamStateRepository(context));
        module.Start();

        var displayCache = new PlatformUserDisplayCache(context);
        var displayCacheUpdater = new TwitchDisplayCacheUpdater(displayCache);
        
        var adapter = new TwitchAdapter(bus, new InMemoryStreamEventTypeRegistry(), displayCacheUpdater);
        await adapter.StartAsync(TestContext.Current.CancellationToken);

        await trigger(adapter, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        return (
            await SnapshotIdentitiesAsync(context),
            await SnapshotMembersAsync(context),
            await SnapshotCacheAsync(context)
        );
    }

    private static Task<MemberIdentitySnapshot[]> SnapshotIdentitiesAsync(VulperonexDbContext context)
    {
        return context.PlatformIdentities
            .OrderBy(identity => identity.Platform)
            .ThenBy(identity => identity.PlatformUserId)
            .Select(identity => new MemberIdentitySnapshot(
                identity.Platform,
                identity.PlatformUserId,
                identity.IsFollower,
                identity.IsSubscriber,
                identity.SubscriptionTier))
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static Task<MemberSnapshot[]> SnapshotMembersAsync(VulperonexDbContext context)
    {
        return context.Members
            .OrderBy(m => m.TotalLoyalty)
            .ThenBy(m => m.CheckInCount)
            .Select(m => new MemberSnapshot(
                m.TotalLoyalty,
                m.CheckInCount))
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static Task<DisplayInfoSnapshot[]> SnapshotCacheAsync(VulperonexDbContext context)
    {
        return context.PlatformUserDisplayInfo
            .OrderBy(c => c.Platform)
            .ThenBy(c => c.PlatformUserId)
            .Select(c => new DisplayInfoSnapshot(
                c.Platform,
                c.PlatformUserId,
                c.DisplayName,
                c.IsSubscriber,
                c.SubscriptionTier,
                c.TotalBitsGiven))
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private sealed record MemberIdentitySnapshot(
        string Platform,
        string PlatformUserId,
        bool IsFollower,
        bool IsSubscriber,
        string? SubscriptionTier);

    private sealed record MemberSnapshot(
        int TotalLoyalty,
        int CheckInCount);

    private sealed record DisplayInfoSnapshot(
        string Platform,
        string PlatformUserId,
        string? DisplayName,
        bool IsSubscriber,
        string? SubscriptionTier,
        long TotalBitsGiven);
}
