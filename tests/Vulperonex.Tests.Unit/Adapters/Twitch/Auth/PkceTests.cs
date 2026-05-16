using System.Net;
using FluentAssertions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
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
        PkceCodeChallenge.FromVerifier("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")
            .Should()
            .Be("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public void Given_CodeVerifierContainsInvalidCharacters_When_ChallengeCreated_Then_ItIsRejected()
    {
        var create = () => PkceCodeChallenge.FromVerifier("non-ascii-\u00e9-verifier-that-is-long-enough-for-rfc7636");

        create.Should().Throw<ArgumentException>();
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

    [Fact]
    public async Task Given_AdapterOAuthState_When_CallbackValidated_Then_StateStoreAndValidatorAreUsed()
    {
        await using var bus = new InMemoryStreamEventBus();
        var adapter = new TwitchAdapter(bus, new InMemoryStreamEventTypeRegistry());
        var state = adapter.CreateOAuthState();

        adapter.ValidateOAuthCallback(new OAuthCallbackRequest(IPAddress.Loopback, "localhost:7979", "/auth/callback", state, "code"), 7979)
            .Should()
            .BeTrue();
        adapter.ValidateOAuthCallback(new OAuthCallbackRequest(IPAddress.Loopback, "localhost:7979", "/auth/callback", state, "code"), 7979)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Given_FirstCallbackPortIsUnavailable_When_PortSelected_Then_NextConfiguredPortIsUsed()
    {
        var selector = new OAuthCallbackPortSelector(port => port is 7980);

        selector.Select().Should().Be(7980);
    }

    [Fact]
    public void Given_NoCallbackPortsAreAvailable_When_PortSelected_Then_UserFacingErrorIsReturned()
    {
        var selector = new OAuthCallbackPortSelector(_ => false);

        var select = selector.Select;

        select.Should().Throw<InvalidOperationException>()
            .WithMessage("*7979*7980*7981*");
    }

    [Fact]
    public void Given_StateIsOlderThanTenMinutes_When_Consumed_Then_ItIsRejectedAndRemoved()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero));
        var store = new PkceStateStore(timeProvider);
        var state = store.Create();

        timeProvider.Advance(TimeSpan.FromMinutes(11));

        store.Consume(state).Should().BeFalse();
        store.Consume(state).Should().BeFalse();
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow += delta;
        }
    }
}
