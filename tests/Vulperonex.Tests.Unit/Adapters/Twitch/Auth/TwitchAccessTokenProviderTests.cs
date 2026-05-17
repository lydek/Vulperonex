using FluentAssertions;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.Auth;

public sealed class TwitchAccessTokenProviderTests
{
    [Fact]
    public async Task Given_CodeExchange_When_Completed_Then_AccessTokenStaysInMemoryAndRefreshTokenIsStored()
    {
        var store = new RecordingTokenStore();
        var endpoint = new RecordingTokenEndpoint(new TwitchTokenResponse("access-1", "refresh-1"));
        var provider = new TwitchAccessTokenProvider(store, endpoint);

        await provider.ExchangeCodeAsync("code-1", "verifier-1", TestContext.Current.CancellationToken);

        provider.AccessToken.Should().Be("access-1");
        endpoint.ExchangeCode.Should().Be("code-1");
        endpoint.ExchangeVerifier.Should().Be("verifier-1");
        store.Stored.Should().ContainSingle().Which.Should().Be(("twitch", "refresh-1"));
    }

    [Fact]
    public async Task Given_StoredRefreshToken_When_StartupRefreshRuns_Then_AccessTokenIsUpdatedInMemory()
    {
        var store = new RecordingTokenStore { RefreshToken = "refresh-old" };
        var endpoint = new RecordingTokenEndpoint(new TwitchTokenResponse("access-new", "refresh-new"));
        var provider = new TwitchAccessTokenProvider(store, endpoint);

        await provider.RefreshOnStartupAsync(TestContext.Current.CancellationToken);

        provider.AccessToken.Should().Be("access-new");
        endpoint.RefreshToken.Should().Be("refresh-old");
        store.Stored.Should().ContainSingle().Which.Should().Be(("twitch", "refresh-new"));
    }

    [Fact]
    public async Task Given_RefreshTokenCannotBeDecrypted_When_StartupRefreshRuns_Then_AuthorizationIsRequiredWithoutCrash()
    {
        var store = new RecordingTokenStore { ThrowOnRead = true };
        var endpoint = new RecordingTokenEndpoint(new TwitchTokenResponse("access-new", "refresh-new"));
        var provider = new TwitchAccessTokenProvider(store, endpoint);

        await provider.RefreshOnStartupAsync(TestContext.Current.CancellationToken);

        provider.AuthorizationRequired.Should().BeTrue();
        provider.AccessToken.Should().BeNull();
        endpoint.RefreshToken.Should().BeNull();
    }

    private sealed class RecordingTokenStore : IOAuthTokenStore
    {
        public string? RefreshToken { get; init; }
        public bool ThrowOnRead { get; init; }
        public List<(string Platform, string Token)> Stored { get; } = [];

        public Task StoreRefreshTokenAsync(string platform, string rawToken, CancellationToken cancellationToken = default)
        {
            Stored.Add((platform, rawToken));
            return Task.CompletedTask;
        }

        public Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead)
            {
                throw new CredentialDecryptionException("Cannot decrypt token.");
            }

            return Task.FromResult(RefreshToken);
        }

        public Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(RefreshToken));
        }
    }

    private sealed class RecordingTokenEndpoint(TwitchTokenResponse response) : ITwitchTokenEndpoint
    {
        public string? ExchangeCode { get; private set; }
        public string? ExchangeVerifier { get; private set; }
        public string? RefreshToken { get; private set; }

        public Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            ExchangeCode = code;
            ExchangeVerifier = codeVerifier;
            return Task.FromResult(response);
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshToken = refreshToken;
            return Task.FromResult(response);
        }
    }
}
