using FluentAssertions;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Events;

public sealed class StreamEventKeysTests
{
    [Fact]
    public void Given_StreamEventKeys_When_Read_Then_AllCanonicalKeysMatchSpec()
    {
        StreamEventKeys.UserSentMessage.Should().Be("user.message");
        StreamEventKeys.UserFollowed.Should().Be("user.followed");
        StreamEventKeys.UserDonated.Should().Be("user.donated");
        StreamEventKeys.UserSubscribed.Should().Be("user.subscribed");
        StreamEventKeys.UserGiftedSubscription.Should().Be("user.gifted_sub");
        StreamEventKeys.ChannelRaided.Should().Be("channel.raided");
        StreamEventKeys.RewardRedeemed.Should().Be("reward.redeemed");
        StreamEventKeys.PlatformConnectionChanged.Should().Be("platform.connection_changed");
    }
}
