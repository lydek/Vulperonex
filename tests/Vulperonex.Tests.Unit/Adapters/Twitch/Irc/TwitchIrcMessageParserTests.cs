using FluentAssertions;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Domain;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.Irc;

public sealed class TwitchIrcMessageParserTests
{
    [Fact]
    public void Given_IrcMessageTags_When_Parsed_Then_UserSentMessageEventAndDisplayHintsAreNormalized()
    {
        var message = new TwitchIrcMessage(
            new Dictionary<string, string>
            {
                ["user-id"] = "42",
                ["display-name"] = "Alice",
                ["color"] = "#12A0ff",
                ["badges"] = "subscriber/12,moderator/1,broadcaster/1,vip/1,bad!ge/1,too/longlonglonglonglonglonglonglonglonglonglonglonglonglonglonglonglong",
                ["user.avatar"] = "https://static-cdn.jtvnw.net/avatar.png",
                ["user.is_subscriber"] = "true",
                ["user.bits_total"] = "100",
            },
            "alice",
            "channel",
            "<b>hello</b>");

        var result = TwitchIrcMessageParser.Parse(message);

        result.Event.Platform.Should().Be("twitch");
        result.Event.User.UserId.Should().Be("42");
        result.Event.User.DisplayName.Should().Be("Alice");
        result.Event.User.Login.Should().Be("alice");
        result.Event.User.Roles.Should().Be(StreamRole.Subscriber | StreamRole.Moderator | StreamRole.Broadcaster | StreamRole.Vip);
        result.Event.MessageText.Should().Be("<b>hello</b>");
        result.DisplayHints.ColorHex.Should().Be("#12A0ff");
        result.DisplayHints.Badges.Should().BeEquivalentTo("subscriber/12", "moderator/1", "broadcaster/1", "vip/1");
        result.DisplayHints.Segments.Should().ContainSingle().Subject.Should().BeEquivalentTo(new { Type = "text", Value = "<b>hello</b>" });
        result.DisplayHints.IsSubscriber.Should().BeTrue();
        result.DisplayHints.TotalBitsGiven.Should().Be(100);
    }

    [Fact]
    public void Given_InvalidDisplayTags_When_Parsed_Then_UnsafeValuesAreDropped()
    {
        var message = new TwitchIrcMessage(
            new Dictionary<string, string>
            {
                ["color"] = "red",
                ["badges"] = "subscriber/<script>",
                ["user.bits_total"] = "-1",
            },
            "alice",
            "channel",
            "hello");

        var result = TwitchIrcMessageParser.Parse(message);

        result.DisplayHints.ColorHex.Should().BeNull();
        result.DisplayHints.Badges.Should().BeEmpty();
        result.DisplayHints.TotalBitsGiven.Should().Be(0);
        TwitchIrcMessageParser.IsAllowedSegmentType("html").Should().BeFalse();
    }

    [Fact]
    public void Given_NumericUserId_When_Parsed_Then_LoginIsTakenFromIrcUserName()
    {
        var message = new TwitchIrcMessage(
            new Dictionary<string, string>
            {
                ["user-id"] = "109565589",
                ["display-name"] = "RotanFox",
            },
            "rotanfox",
            "channel",
            "hello");

        var result = TwitchIrcMessageParser.Parse(message);

        result.Event.User.UserId.Should().Be("109565589");
        result.Event.User.DisplayName.Should().Be("RotanFox");
        result.Event.User.Login.Should().Be("rotanfox");
    }

    [Theory]
    [InlineData("#fff")]
    [InlineData("#ffffff80")]
    [InlineData("red")]
    public void Given_UnsupportedColorFormat_When_Parsed_Then_ColorIsDropped(string color)
    {
        var message = new TwitchIrcMessage(
            new Dictionary<string, string> { ["color"] = color },
            "alice",
            "channel",
            "hello");

        TwitchIrcMessageParser.Parse(message).DisplayHints.ColorHex.Should().BeNull();
    }
}
