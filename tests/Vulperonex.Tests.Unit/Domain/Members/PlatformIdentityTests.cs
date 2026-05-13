using FluentAssertions;
using Vulperonex.Domain.Members;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Members;

public sealed class PlatformIdentityTests
{
    [Fact]
    public void Given_PlatformAndUserId_When_Created_Then_ValuesAreTrimmedAndExposed()
    {
        var identity = PlatformIdentity.Create(" twitch ", " 12345 ");

        identity.Platform.Should().Be("twitch");
        identity.PlatformUserId.Should().Be("12345");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_InvalidPlatform_When_Created_Then_ArgumentExceptionIsThrown(string platform)
    {
        var act = () => PlatformIdentity.Create(platform, "12345");

        act.Should().Throw<ArgumentException>().WithParameterName("platform");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Given_InvalidPlatformUserId_When_Created_Then_ArgumentExceptionIsThrown(string platformUserId)
    {
        var act = () => PlatformIdentity.Create("twitch", platformUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("platformUserId");
    }
}
