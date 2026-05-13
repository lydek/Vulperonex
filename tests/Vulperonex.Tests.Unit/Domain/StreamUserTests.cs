using FluentAssertions;
using Vulperonex.Domain;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain;

public sealed class StreamUserTests
{
    [Fact]
    public void Given_StreamUserValues_When_Constructed_Then_PlatformUserIdAndDisplayNameAreExposed()
    {
        var user = new StreamUser("twitch", "12345", "alice");

        user.Platform.Should().Be("twitch");
        user.UserId.Should().Be("12345");
        user.DisplayName.Should().Be("alice");
    }

    [Fact]
    public void Given_SameStreamUserValues_When_Compared_Then_RecordValueEqualityApplies()
    {
        var first = new StreamUser("twitch", "12345", "alice");
        var second = new StreamUser("twitch", "12345", "alice");

        first.Should().Be(second);
    }

    [Fact]
    public void Given_StreamUser_When_CopiedWithExpression_Then_OriginalInstanceRemainsUnchanged()
    {
        var original = new StreamUser("twitch", "12345", "alice");
        var updated = original with { DisplayName = "AliceWonder" };

        original.DisplayName.Should().Be("alice");
        updated.DisplayName.Should().Be("AliceWonder");
    }
}
