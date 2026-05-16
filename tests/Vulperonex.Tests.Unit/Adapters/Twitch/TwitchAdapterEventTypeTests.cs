using FluentAssertions;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch;

public sealed class TwitchAdapterEventTypeTests
{
    [Fact]
    public async Task Given_TwitchAdapter_When_Started_Then_ItRegistersSevenWorkflowVisibleEventTypesAndSystemConnectionEvent()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new TwitchAdapter(bus, registry);

        await adapter.StartAsync(TestContext.Current.CancellationToken);

        registry.GetAll().Select(metadata => metadata.Key).Should().BeEquivalentTo(
            StreamEventKeys.UserSentMessage,
            StreamEventKeys.UserFollowed,
            StreamEventKeys.UserDonated,
            StreamEventKeys.UserSubscribed,
            StreamEventKeys.UserGiftedSubscription,
            StreamEventKeys.ChannelRaided,
            StreamEventKeys.RewardRedeemed);
        registry.IsKnown(StreamEventKeys.PlatformConnectionChanged).Should().BeTrue();
        registry.IsKnownForWorkflow(StreamEventKeys.PlatformConnectionChanged).Should().BeFalse();
    }

    [Fact]
    public async Task Given_TwitchAdapter_When_StartedTwice_Then_EventTypeRegistrationIsIdempotent()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new TwitchAdapter(bus, registry);

        await adapter.StartAsync(TestContext.Current.CancellationToken);
        await adapter.StartAsync(TestContext.Current.CancellationToken);

        registry.GetAll().Should().HaveCount(7);
    }

    [Fact]
    public async Task Given_IrcMessage_When_PublishedThroughAdapter_Then_ParserDedupAndDisplayCacheAreUsed()
    {
        await using var bus = new InMemoryStreamEventBus();
        var cache = new RecordingPlatformUserInfoCache();
        var adapter = new TwitchAdapter(
            bus,
            new InMemoryStreamEventTypeRegistry(),
            new TwitchDisplayCacheUpdater(cache));
        var received = new List<UserSentMessageEvent>();
        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });
        var message = new TwitchIrcMessage(
            new Dictionary<string, string>
            {
                ["msg-id"] = "msg-1",
                ["user-id"] = "42",
                ["display-name"] = "Alice",
                ["color"] = "#ffffff",
                ["badges"] = "subscriber/12",
                ["user.bits_total"] = "50",
            },
            "alice",
            "channel",
            "hello");

        await adapter.StartAsync(TestContext.Current.CancellationToken);
        await adapter.PublishIrcMessageAsync(message, TestContext.Current.CancellationToken);
        await adapter.PublishIrcMessageAsync(message, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().ContainSingle().Subject.EventId.Should().Be("msg-1");
        cache.Updates.Should().ContainSingle();
        cache.Current.DisplayName.Should().Be("Alice");
        cache.Current.ColorHex.Should().Be("#ffffff");
        cache.Current.IsSubscriber.Should().BeTrue();
        cache.Current.TotalBitsGiven.Should().Be(50);
    }

    [Fact]
    public async Task Given_SubscriptionPayload_When_PublishedThroughAdapter_Then_DisplayCacheContainsSubscriberBadge()
    {
        await using var bus = new InMemoryStreamEventBus();
        var cache = new RecordingPlatformUserInfoCache();
        var adapter = new TwitchAdapter(
            bus,
            new InMemoryStreamEventTypeRegistry(),
            new TwitchDisplayCacheUpdater(cache));

        await adapter.StartAsync(TestContext.Current.CancellationToken);
        await adapter.PublishMockPayloadAsync(
            new TwitchMockPayload(
                TwitchMockPayloadKind.Subscribed,
                new("twitch", "42", "Alice"),
                Tier: "1000",
                SourceEventId: "sub-1"),
            TestContext.Current.CancellationToken);

        cache.Current.IsSubscriber.Should().BeTrue();
        cache.Current.SubscriptionTier.Should().Be("1000");
        cache.Current.Badges.Should().Contain("subscriber/1000");
    }

    private sealed class RecordingPlatformUserInfoCache : IPlatformUserInfoCache
    {
        public PlatformUserDisplayInfo Current { get; private set; } = new(
            "twitch",
            "42",
            null,
            null,
            null,
            [],
            IsSubscriber: false,
            SubscriptionTier: null,
            TotalBitsGiven: 0,
            DateTimeOffset.UnixEpoch);

        public List<PlatformUserDisplayInfo> Updates { get; } = [];

        public Task<PlatformUserDisplayInfo?> GetAsync(string platform, string platformUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlatformUserDisplayInfo?>(Current);
        }

        public Task<PlatformUserDisplayInfo> UpdateAsync(
            string platform,
            string platformUserId,
            Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
            CancellationToken cancellationToken = default)
        {
            Current = updater(Current with { Platform = platform, PlatformUserId = platformUserId });
            Updates.Add(Current);
            return Task.FromResult(Current);
        }
    }
}
