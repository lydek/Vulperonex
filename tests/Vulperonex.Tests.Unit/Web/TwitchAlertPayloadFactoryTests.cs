using FluentAssertions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Web.TwitchAuth;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class TwitchAlertPayloadFactoryTests
{
    [Fact]
    public void Follow_maps_user_and_kind()
    {
        var p = TwitchAlertPayloadFactory.Follow("42", "alice", "Alice", "src-1");

        p.Kind.Should().Be(TwitchMockPayloadKind.Followed);
        p.User.UserId.Should().Be("42");
        p.User.Login.Should().Be("alice");
        p.User.DisplayName.Should().Be("Alice");
        p.SourceEventId.Should().Be("src-1");
    }

    [Fact]
    public void Subscribe_carries_tier()
    {
        var p = TwitchAlertPayloadFactory.Subscribe("42", "alice", "Alice", "1000");

        p.Kind.Should().Be(TwitchMockPayloadKind.Subscribed);
        p.Tier.Should().Be("1000");
    }

    [Fact]
    public void GiftSub_carries_tier_and_count()
    {
        var p = TwitchAlertPayloadFactory.GiftSub("42", "alice", "Alice", "2000", 5);

        p.Kind.Should().Be(TwitchMockPayloadKind.GiftedSubscription);
        p.Tier.Should().Be("2000");
        p.GiftCount.Should().Be(5);
    }

    [Fact]
    public void Cheer_carries_bits()
    {
        var p = TwitchAlertPayloadFactory.Cheer("42", "alice", "Alice", 250);

        p.Kind.Should().Be(TwitchMockPayloadKind.Donated);
        p.TotalBitsGiven.Should().Be(250);
    }

    [Fact]
    public void Raid_uses_raider_as_user_and_carries_viewers()
    {
        var p = TwitchAlertPayloadFactory.Raid("77", "raider", "Raider", 321);

        p.Kind.Should().Be(TwitchMockPayloadKind.Raided);
        p.User.UserId.Should().Be("77");
        p.User.Login.Should().Be("raider");
        p.ViewerCount.Should().Be(321);
    }

    [Fact]
    public void Redemption_carries_reward_fields_and_dedup_id()
    {
        var p = TwitchAlertPayloadFactory.Redemption("42", "alice", "Alice", "reward-3", "Hydrate", "redemption-9");

        p.Kind.Should().Be(TwitchMockPayloadKind.RewardRedeemed);
        p.RewardId.Should().Be("reward-3");
        p.RewardTitle.Should().Be("Hydrate");
        p.RedemptionId.Should().Be("redemption-9");
        p.SourceEventId.Should().Be("redemption-9");
    }

    [Fact]
    public void Anonymous_user_falls_back_to_anonymous_id()
    {
        var p = TwitchAlertPayloadFactory.Cheer(null, null, null, 100);

        p.User.UserId.Should().Be("anonymous");
        p.User.DisplayName.Should().Be("Anonymous");
    }
}
