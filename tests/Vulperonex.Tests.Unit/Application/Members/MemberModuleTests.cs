using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Members;

public sealed class MemberModuleTests
{
    [Fact]
    public async Task Given_UserSentMessageEvent_When_Published_Then_MemberResolverCreatesPlatformIdentity()
    {
        await using var bus = new InMemoryStreamEventBus();
        var resolver = new RecordingMemberResolver();
        using var module = new MemberModule(bus, resolver, new RecordingMemberStreamStateRepository());
        await module.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            MessageText = "hello",
        }, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        resolver.Identities.Should().ContainSingle().Subject.Should().Be(PlatformIdentity.Create("twitch", "alice"));
    }

    [Fact]
    public void Given_MemberModule_When_Inspected_Then_ItIsHostedService()
    {
        typeof(MemberModule).Should().BeAssignableTo<IHostedService>();
    }

    [Theory]
    [MemberData(nameof(UserEventsWithoutStateUpdates))]
    public async Task Given_MvpUserEvent_When_Published_Then_MemberResolverCreatesPlatformIdentity(IStreamEvent streamEvent)
    {
        await using var bus = new InMemoryStreamEventBus();
        var resolver = new RecordingMemberResolver();
        using var module = new MemberModule(bus, resolver, new RecordingMemberStreamStateRepository());
        await module.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        resolver.Identities.Should().ContainSingle().Subject.Should().Be(PlatformIdentity.Create("twitch", "alice"));
    }

    [Fact]
    public async Task Given_SubscriptionEvent_When_PublishedTwice_Then_StateUpdateIsAppliedOnceBySourceEventId()
    {
        await using var bus = new InMemoryStreamEventBus();
        var resolver = new RecordingMemberResolver();
        var states = new RecordingMemberStreamStateRepository();
        using var module = new MemberModule(bus, resolver, states);
        await module.StartAsync(TestContext.Current.CancellationToken);
        var streamEvent = new UserSubscribedEvent
        {
            EventId = "event-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            Tier = "1000",
        };

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        resolver.Identities.Should().ContainSingle();
        states.Subscriptions.Should().ContainSingle().Which.Should().Be((PlatformIdentity.Create("twitch", "alice"), "1000"));
    }

    private sealed class RecordingMemberResolver : IMemberResolver
    {
        public List<PlatformIdentity> Identities { get; } = [];

        public Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            Identities.Add(identity);
            return Task.FromResult("01HX0000000000000000000000");
        }
    }

    private sealed class RecordingMemberStreamStateRepository : IMemberStreamStateRepository
    {
        public List<PlatformIdentity> Followers { get; } = [];
        public List<(PlatformIdentity Identity, string Tier)> Subscriptions { get; } = [];
        public List<PlatformIdentity> CheckIns { get; } = [];

        public Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            Followers.Add(identity);
            return Task.CompletedTask;
        }

        public Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default)
        {
            Subscriptions.Add((identity, tier));
            return Task.CompletedTask;
        }

        public Task<int> IncrementCheckInAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            CheckIns.Add(identity);
            return Task.FromResult(CheckIns.Count);
        }
    }

    public static TheoryData<IStreamEvent> UserEventsWithoutStateUpdates()
    {
        var user = new StreamUser("twitch", "alice", "Alice");
        return new TheoryData<IStreamEvent>
        {
            new UserDonatedEvent
            {
                EventId = "bits-1",
                Platform = "twitch",
                User = user,
                TotalBitsGiven = 100,
            },
            new UserGiftedSubscriptionEvent
            {
                EventId = "gift-1",
                Platform = "twitch",
                User = user,
                Tier = "1000",
                GiftCount = 2,
            },
            new ChannelRaidedEvent
            {
                EventId = "raid-1",
                Platform = "twitch",
                User = user,
                ViewerCount = 25,
            },
            new RewardRedeemedEvent
            {
                EventId = "reward-1",
                Platform = "twitch",
                User = user,
                RewardId = "highlight",
                RewardTitle = "Highlight",
            },
        };
    }
}
