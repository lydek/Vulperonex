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
    public async Task Given_AccessTokenIsStillFresh_When_Ensured_Then_DoesNotRefreshAgain()
    {
        var store = new RecordingTokenStore { RefreshToken = "refresh-old" };
        var endpoint = new RecordingTokenEndpoint(new TwitchTokenResponse("access-new", "refresh-new", ExpiresIn: 600));
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero));
        var provider = new TwitchAccessTokenProvider(store, endpoint, timeProvider);

        await provider.RefreshOnStartupAsync(TestContext.Current.CancellationToken);
        await provider.EnsureValidAccessTokenAsync(TestContext.Current.CancellationToken);

        provider.AccessToken.Should().Be("access-new");
        endpoint.RefreshCount.Should().Be(1);
        store.Stored.Should().ContainSingle().Which.Should().Be(("twitch", "refresh-new"));
    }

    [Fact]
    public async Task Given_AccessTokenIsExpired_When_Ensured_Then_RefreshesBeforeUse()
    {
        var store = new RecordingTokenStore { RefreshToken = "refresh-old" };
        var endpoint = new SequencedTokenEndpoint(
            new TwitchTokenResponse("access-old", "refresh-mid", ExpiresIn: 300),
            new TwitchTokenResponse("access-new", "refresh-new", ExpiresIn: 300));
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero));
        var provider = new TwitchAccessTokenProvider(store, endpoint, timeProvider);

        await provider.RefreshOnStartupAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        await provider.EnsureValidAccessTokenAsync(TestContext.Current.CancellationToken);

        provider.AccessToken.Should().Be("access-new");
        endpoint.RefreshCount.Should().Be(2);
        store.Stored.Should().Contain(("twitch", "refresh-new"));
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

    [Fact]
    public async Task Given_ExpiredToken_When_ManyCallersEnsureConcurrently_Then_OnlyOneRefreshRuns()
    {
        // Regression for the ce5c6aa race: Twitch rotates the refresh token on
        // every refresh, so two concurrent refreshes make the loser persist a
        // stale rotated-away token and silently de-authorize the app. The
        // provider must single-flight refreshes behind its lock.
        var store = new RecordingTokenStore { RefreshToken = "refresh-old" };
        var endpoint = new BlockingTokenEndpoint(new TwitchTokenResponse("access-new", "refresh-new", ExpiresIn: 600));
        var provider = new TwitchAccessTokenProvider(store, endpoint);

        var callers = Enumerable.Range(0, 8)
            .Select(_ => provider.EnsureValidAccessTokenAsync(TestContext.Current.CancellationToken))
            .ToArray();

        // All callers are now queued; release the single in-flight refresh.
        await endpoint.WaitForFirstRefreshAsync(TestContext.Current.CancellationToken);
        endpoint.ReleaseRefresh();
        await Task.WhenAll(callers);

        endpoint.RefreshCount.Should().Be(1, "concurrent callers must share one refresh, never race the rotating refresh token");
        provider.AccessToken.Should().Be("access-new");
        store.Stored.Should().ContainSingle().Which.Should().Be(("twitch", "refresh-new"));
    }

    [Fact]
    public async Task Given_RefreshEndpointRejectsToken_When_RefreshRuns_Then_AuthorizationRequiredAndTokenCleared()
    {
        var store = new RecordingTokenStore { RefreshToken = "refresh-revoked" };
        var endpoint = new ThrowingTokenEndpoint();
        var provider = new TwitchAccessTokenProvider(store, endpoint);

        await provider.RefreshAsync(TestContext.Current.CancellationToken);

        provider.AuthorizationRequired.Should().BeTrue();
        provider.AccessToken.Should().BeNull();
        // The invalid refresh token must be cleared so we stop hammering id.twitch.tv.
        store.Stored.Should().ContainSingle().Which.Should().Be(("twitch", string.Empty));
    }

    private sealed class BlockingTokenEndpoint(TwitchTokenResponse response) : ITwitchTokenEndpoint
    {
        private readonly TaskCompletionSource _firstRefreshStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _refreshCount;

        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public Task WaitForFirstRefreshAsync(CancellationToken cancellationToken)
            => _firstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        public void ReleaseRefresh() => _release.TrySetResult();

        public Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
            => Task.FromResult(response);

        public async Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _refreshCount);
            _firstRefreshStarted.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return response;
        }
    }

    private sealed class ThrowingTokenEndpoint : ITwitchTokenEndpoint
    {
        public Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("invalid grant");

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("invalid grant");
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

        public Task ClearRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTokenEndpoint(TwitchTokenResponse response) : ITwitchTokenEndpoint
    {
        public string? ExchangeCode { get; private set; }
        public string? ExchangeVerifier { get; private set; }
        public string? RefreshToken { get; private set; }
        public int RefreshCount { get; private set; }

        public Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            ExchangeCode = code;
            ExchangeVerifier = codeVerifier;
            return Task.FromResult(response);
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshToken = refreshToken;
            RefreshCount++;
            return Task.FromResult(response);
        }
    }

    private sealed class SequencedTokenEndpoint(params TwitchTokenResponse[] responses) : ITwitchTokenEndpoint
    {
        private int _nextResponseIndex;
        public int RefreshCount { get; private set; }

        public Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(responses[Math.Min(_nextResponseIndex++, responses.Length - 1)]);
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            return Task.FromResult(responses[Math.Min(_nextResponseIndex++, responses.Length - 1)]);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
