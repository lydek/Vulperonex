using System.Collections.Concurrent;
using FluentAssertions;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Application.EventBus;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Simulation;

public sealed class SimulationAdapterTests
{
    [Fact]
    public async Task Given_SimulationAdapter_When_Started_Then_ItRegistersSevenWorkflowVisibleEventTypes()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new SimulationAdapter(bus, registry);

        await adapter.StartAsync(TestContext.Current.CancellationToken);

        registry.GetAll().Select(metadata => metadata.Key).Should().BeEquivalentTo(
            StreamEventKeys.UserSentMessage,
            StreamEventKeys.UserFollowed,
            StreamEventKeys.UserDonated,
            StreamEventKeys.UserSubscribed,
            StreamEventKeys.UserGiftedSubscription,
            StreamEventKeys.ChannelRaided,
            StreamEventKeys.RewardRedeemed);
        registry.IsKnownForWorkflow(StreamEventKeys.PlatformConnectionChanged).Should().BeFalse();
    }

    [Fact]
    public async Task Given_EachSimulationKind_When_Simulated_Then_BusReceivesMatchingConcreteEvents()
    {
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());
        var received = new ConcurrentBag<IStreamEvent>();
        bus.Subscribe<IStreamEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });

        foreach (var request in CreateAllRequests())
        {
            await adapter.SimulateAsync(request, TestContext.Current.CancellationToken);
        }

        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Select(streamEvent => streamEvent.EventTypeKey).Should().BeEquivalentTo(
            StreamEventKeys.UserSentMessage,
            StreamEventKeys.UserFollowed,
            StreamEventKeys.UserDonated,
            StreamEventKeys.UserSubscribed,
            StreamEventKeys.UserGiftedSubscription,
            StreamEventKeys.ChannelRaided,
            StreamEventKeys.RewardRedeemed);
    }

    [Fact]
    public async Task Given_MessageSimulation_When_Simulated_Then_MessageEventPreservesUserAndText()
    {
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());
        var received = new ConcurrentBag<UserSentMessageEvent>();
        var user = new StreamUser("twitch", "alice", "Alice");
        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });

        await adapter.SimulateAsync(SimulationRequest.Message("twitch", user, "hello"), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        var streamEvent = received.Should().ContainSingle().Subject;
        streamEvent.Platform.Should().Be("twitch");
        streamEvent.User.Should().Be(user);
        streamEvent.MessageText.Should().Be("hello");
    }

    [Fact]
    public async Task Given_SubscriptionSimulation_When_Simulated_Then_SubscriptionEventPreservesTier()
    {
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());
        var received = new ConcurrentBag<UserSubscribedEvent>();
        var user = new StreamUser("twitch", "alice", "Alice");
        bus.Subscribe<UserSubscribedEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });

        await adapter.SimulateAsync(SimulationRequest.Subscribed("twitch", user, "1000"), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().ContainSingle().Subject.Tier.Should().Be("1000");
    }

    private static IEnumerable<SimulationRequest> CreateAllRequests()
    {
        var user = new StreamUser("twitch", "alice", "Alice");

        yield return SimulationRequest.Message("twitch", user, "hello");
        yield return SimulationRequest.Followed("twitch", user);
        yield return SimulationRequest.Donated("twitch", user, totalBitsGiven: 100);
        yield return SimulationRequest.Subscribed("twitch", user, tier: "1000");
        yield return SimulationRequest.GiftedSubscription("twitch", user, tier: "1000", giftCount: 1);
        yield return SimulationRequest.Raided("twitch", user, viewerCount: 10);
        yield return SimulationRequest.RewardRedeemed("twitch", user, rewardId: "reward-1", rewardTitle: "Highlight");
    }
}
