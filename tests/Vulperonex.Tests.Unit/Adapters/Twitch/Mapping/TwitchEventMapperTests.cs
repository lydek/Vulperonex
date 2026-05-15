using System.Collections.Concurrent;
using FluentAssertions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.Mapping;

public sealed class TwitchEventMapperTests
{
    [Fact]
    public void Given_AllMvpPayloadKinds_When_Mapped_Then_ConcreteDomainEventsUseTwitchPlatform()
    {
        var user = new StreamUser("twitch", "alice", "Alice");

        CreateAllPayloads(user)
            .Select(TwitchEventMapper.Map)
            .Should()
            .AllSatisfy(streamEvent => streamEvent.Platform.Should().Be("twitch"))
            .And.Subject.Select(streamEvent => streamEvent.EventTypeKey).Should().BeEquivalentTo(
                StreamEventKeys.UserSentMessage,
                StreamEventKeys.UserFollowed,
                StreamEventKeys.UserDonated,
                StreamEventKeys.UserSubscribed,
                StreamEventKeys.UserGiftedSubscription,
                StreamEventKeys.ChannelRaided,
                StreamEventKeys.RewardRedeemed);
    }

    [Fact]
    public async Task Given_MockPayload_When_PublishedThroughAdapter_Then_BusSubscriberReceivesMappedEvent()
    {
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new TwitchAdapter(bus, new InMemoryStreamEventTypeRegistry());
        var received = new ConcurrentBag<UserSentMessageEvent>();
        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });

        await adapter.PublishMockPayloadAsync(
            new TwitchMockPayload(TwitchMockPayloadKind.Message, new StreamUser("twitch", "alice", "Alice"), MessageText: "hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().ContainSingle().Subject.MessageText.Should().Be("hello");
    }

    private static IEnumerable<TwitchMockPayload> CreateAllPayloads(StreamUser user)
    {
        yield return new TwitchMockPayload(TwitchMockPayloadKind.Message, user, MessageText: "hello");
        yield return new TwitchMockPayload(TwitchMockPayloadKind.Followed, user);
        yield return new TwitchMockPayload(TwitchMockPayloadKind.Donated, user, TotalBitsGiven: 100);
        yield return new TwitchMockPayload(TwitchMockPayloadKind.Subscribed, user, Tier: "1000");
        yield return new TwitchMockPayload(TwitchMockPayloadKind.GiftedSubscription, user, Tier: "1000", GiftCount: 2);
        yield return new TwitchMockPayload(TwitchMockPayloadKind.Raided, user, ViewerCount: 10);
        yield return new TwitchMockPayload(TwitchMockPayloadKind.RewardRedeemed, user, RewardId: "reward-1", RewardTitle: "Highlight");
    }
}
