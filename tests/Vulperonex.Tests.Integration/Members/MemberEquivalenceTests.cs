using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Members;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class MemberEquivalenceTests
{
    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchMessage_When_Published_Then_MemberDatabaseStateMatches()
    {
        var simulationSnapshot = await RunSimulationAsync();
        var twitchSnapshot = await RunTwitchAsync();

        twitchSnapshot.Should().BeEquivalentTo(simulationSnapshot);
    }

    private static async Task<MemberIdentitySnapshot[]> RunSimulationAsync()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(bus, new MemberResolver(context), new MemberStreamStateRepository(context));
        module.Start();
        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());

        await adapter.SimulateAsync(
            SimulationRequest.Message("twitch", new StreamUser("twitch", "alice", "Alice"), "!hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        return await SnapshotAsync(context);
    }

    private static async Task<MemberIdentitySnapshot[]> RunTwitchAsync()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(bus, new MemberResolver(context), new MemberStreamStateRepository(context));
        module.Start();
        var adapter = new TwitchAdapter(bus, new InMemoryStreamEventTypeRegistry());

        await adapter.PublishMockPayloadAsync(
            new TwitchMockPayload(TwitchMockPayloadKind.Message, new StreamUser("twitch", "alice", "Alice"), MessageText: "!hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        return await SnapshotAsync(context);
    }

    private static Task<MemberIdentitySnapshot[]> SnapshotAsync(Vulperonex.Infrastructure.Data.VulperonexDbContext context)
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

    private sealed record MemberIdentitySnapshot(
        string Platform,
        string PlatformUserId,
        bool IsFollower,
        bool IsSubscriber,
        string? SubscriptionTier);
}
