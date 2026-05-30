using System.Text.Json;
using FluentAssertions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.EventSub;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch;

public sealed class TwitchEventSubMapperTests
{
    [Fact]
    public void Given_ChatMessageEvent_When_Mapped_Then_TagsAndTextMatch()
    {
        var evt = Parse("""
        {
            "broadcaster_user_id": "100",
            "broadcaster_user_login": "streamer",
            "chatter_user_id": "42",
            "chatter_user_name": "Alice",
            "chatter_user_login": "alice",
            "message_id": "msg-1",
            "color": "#ffffff",
            "badges": [
                { "set_id": "subscriber", "id": "12" },
                { "set_id": "moderator", "id": "1" }
            ],
            "cheer": { "bits": 50 },
            "message": { "text": "hello world" }
        }
        """);

        var irc = TwitchEventSubMapper.ToIrcMessage(evt, "envelope-id");

        irc.Text.Should().Be("hello world");
        irc.UserName.Should().Be("alice");
        irc.Channel.Should().Be("streamer");
        irc.Tags["msg-id"].Should().Be("msg-1");
        irc.Tags["user-id"].Should().Be("42");
        irc.Tags["display-name"].Should().Be("Alice");
        irc.Tags["color"].Should().Be("#ffffff");
        irc.Tags["badges"].Should().Be("subscriber/12,moderator/1");
        irc.Tags["bits"].Should().Be("50");
    }

    [Fact]
    public void Given_ChatMessageWithoutOwnId_When_Mapped_Then_FallbackMessageIdUsed()
    {
        var evt = Parse("""
        {
            "chatter_user_id": "42",
            "chatter_user_name": "Alice",
            "message": { "text": "hi" }
        }
        """);

        var irc = TwitchEventSubMapper.ToIrcMessage(evt, "envelope-id");

        irc.Tags["msg-id"].Should().Be("envelope-id");
        irc.Tags.Should().NotContainKey("color");
        irc.Tags.Should().NotContainKey("badges");
    }

    [Fact]
    public void Given_FollowEvent_When_Mapped_Then_FollowedPayload()
    {
        var evt = Parse("""
        { "user_id": "42", "user_name": "Alice", "user_login": "alice" }
        """);

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.FollowType, evt, "msg-1");

        payload.Should().NotBeNull();
        payload!.Kind.Should().Be(TwitchMockPayloadKind.Followed);
        payload.User.UserId.Should().Be("42");
        payload.User.DisplayName.Should().Be("Alice");
        payload.SourceEventId.Should().Be("msg-1");
    }

    [Fact]
    public void Given_SubscribeEvent_When_Mapped_Then_TierCarried()
    {
        var evt = Parse("""{ "user_id": "42", "user_name": "Alice", "tier": "1000" }""");

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.SubscribeType, evt, "msg-1");

        payload!.Kind.Should().Be(TwitchMockPayloadKind.Subscribed);
        payload.Tier.Should().Be("1000");
    }

    [Fact]
    public void Given_GiftEvent_When_Mapped_Then_TierAndCountCarried()
    {
        var evt = Parse("""{ "user_id": "42", "user_name": "Alice", "tier": "2000", "total": 5 }""");

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.SubscriptionGiftType, evt, "msg-1");

        payload!.Kind.Should().Be(TwitchMockPayloadKind.GiftedSubscription);
        payload.Tier.Should().Be("2000");
        payload.GiftCount.Should().Be(5);
    }

    [Fact]
    public void Given_CheerEvent_When_Mapped_Then_BitsCarried()
    {
        var evt = Parse("""{ "user_id": "42", "user_name": "Alice", "bits": 250 }""");

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.CheerType, evt, "msg-1");

        payload!.Kind.Should().Be(TwitchMockPayloadKind.Donated);
        payload.TotalBitsGiven.Should().Be(250);
    }

    [Fact]
    public void Given_AnonymousCheer_When_Mapped_Then_AnonymousUser()
    {
        var evt = Parse("""{ "user_name": null, "bits": 100 }""");

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.CheerType, evt, "msg-1");

        payload!.User.UserId.Should().Be("anonymous");
        payload.TotalBitsGiven.Should().Be(100);
    }

    [Fact]
    public void Given_RaidEvent_When_Mapped_Then_ViewerCountFromRaider()
    {
        var evt = Parse("""
        {
            "from_broadcaster_user_id": "77",
            "from_broadcaster_user_name": "Raider",
            "from_broadcaster_user_login": "raider",
            "viewers": 321
        }
        """);

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.RaidType, evt, "msg-1");

        payload!.Kind.Should().Be(TwitchMockPayloadKind.Raided);
        payload.User.UserId.Should().Be("77");
        payload.ViewerCount.Should().Be(321);
    }

    [Fact]
    public void Given_RewardRedemptionEvent_When_Mapped_Then_RewardFieldsCarried()
    {
        var evt = Parse("""
        {
            "user_id": "42",
            "user_name": "Alice",
            "id": "redemption-9",
            "reward": { "id": "reward-3", "title": "Hydrate" }
        }
        """);

        var payload = TwitchEventSubMapper.ToMockPayload(TwitchEventSubMapper.RewardRedemptionAddType, evt, "msg-1");

        payload!.Kind.Should().Be(TwitchMockPayloadKind.RewardRedeemed);
        payload.RewardId.Should().Be("reward-3");
        payload.RewardTitle.Should().Be("Hydrate");
        payload.RedemptionId.Should().Be("redemption-9");
    }

    [Fact]
    public void Given_UnknownSubscriptionType_When_Mapped_Then_Null()
    {
        var evt = Parse("""{ "user_id": "42" }""");

        TwitchEventSubMapper.ToMockPayload("channel.unknown.thing", evt, "msg-1").Should().BeNull();
    }

    private static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
