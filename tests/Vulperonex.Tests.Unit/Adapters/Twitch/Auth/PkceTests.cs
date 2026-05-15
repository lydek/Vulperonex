using System.Net;
using FluentAssertions;
using Vulperonex.Adapters.Twitch.Auth;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.Auth;

public sealed class PkceTests
{
    [Fact]
    public void Given_StateStore_When_StateCreated_Then_ItIsBase64UrlAndSingleUse()
    {
        var store = new PkceStateStore(TimeProvider.System);

        var state = store.Create();

        state.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        store.Consume(state).Should().BeTrue();
        store.Consume(state).Should().BeFalse();
    }

    [Fact]
    public void Given_CodeVerifier_When_ChallengeCreated_Then_ItUsesSha256Base64Url()
    {
        PkceCodeChallenge.FromVerifier("verifier")
            .Should()
            .Be("iMnq5o6zALKXGivsnlom_0F5_WYda32GHkxlV7mq7hQ");
    }

    [Fact]
    public void Given_CallbackRequest_When_Validated_Then_LoopbackHostPathAndStateMustMatch()
    {
        var store = new PkceStateStore(TimeProvider.System);
        var state = store.Create();
        var validator = new OAuthCallbackValidator(7979, store);

        validator.IsValid(new OAuthCallbackRequest(IPAddress.Loopback, "localhost:7979", "/auth/callback", state, "code"))
            .Should()
            .BeTrue();
        validator.IsValid(new OAuthCallbackRequest(IPAddress.Parse("192.168.1.10"), "localhost:7979", "/auth/callback", store.Create(), "code"))
            .Should()
            .BeFalse();
    }
}
