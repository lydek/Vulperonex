using FluentAssertions;
using Vulperonex.Web.Security;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class OverlayLanAccessKeyProviderTests
{
    [Fact]
    public void Given_GeneratedKeys_When_Compared_Then_AreNonEmptyAndUnique()
    {
        var a = OverlayLanAccessKeyProvider.GenerateKey();
        var b = OverlayLanAccessKeyProvider.GenerateKey();

        a.Should().NotBeNullOrWhiteSpace();
        b.Should().NotBeNullOrWhiteSpace();
        a.Should().NotBe(b);
    }

    [Fact]
    public void Given_KeySet_When_ValidatingMatchingCandidate_Then_ReturnsTrue()
    {
        var provider = new OverlayLanAccessKeyProvider();
        provider.SetKey("secret-key");

        provider.Validate("secret-key").Should().BeTrue();
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("")]
    [InlineData(null)]
    public void Given_KeySet_When_ValidatingNonMatchingCandidate_Then_ReturnsFalse(string? candidate)
    {
        var provider = new OverlayLanAccessKeyProvider();
        provider.SetKey("secret-key");

        provider.Validate(candidate).Should().BeFalse();
    }

    [Fact]
    public void Given_NoKey_When_Validating_Then_AlwaysFalse()
    {
        var provider = new OverlayLanAccessKeyProvider();

        provider.Validate("anything").Should().BeFalse();
    }
}
