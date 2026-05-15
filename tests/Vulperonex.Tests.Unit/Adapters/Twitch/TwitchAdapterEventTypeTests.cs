using FluentAssertions;
using Vulperonex.Adapters.Twitch;
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
}
