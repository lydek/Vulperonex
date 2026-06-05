using Vulperonex.Application.Auth;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class TwitchAccessTokenProvider(
    IOAuthTokenStore tokenStore,
    ITwitchTokenEndpoint tokenEndpoint,
    TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public string? AccessToken => Volatile.Read(ref _accessToken);

    public bool AuthorizationRequired { get; private set; }

    public async Task ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var response = await tokenEndpoint.ExchangeCodeAsync(code, codeVerifier, cancellationToken);
        StoreAccessToken(response);
        await tokenStore.StoreRefreshTokenAsync("twitch", response.RefreshToken, cancellationToken);
    }

    public async Task RefreshOnStartupAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task EnsureValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (HasUsableAccessToken())
        {
            return;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!HasUsableAccessToken())
            {
                await RefreshAsync(cancellationToken);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        string? refreshToken;
        try
        {
            refreshToken = await tokenStore.GetRefreshTokenAsync("twitch", cancellationToken);
        }
        catch (CredentialDecryptionException)
        {
            AuthorizationRequired = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        try
        {
            var response = await tokenEndpoint.RefreshAsync(refreshToken, cancellationToken);
            StoreAccessToken(response);
            AuthorizationRequired = false;
            await tokenStore.StoreRefreshTokenAsync("twitch", response.RefreshToken, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // If the refresh token is expired, revoked or invalid, set AuthorizationRequired = true
            // and gracefully set accessToken to null so orchestrators can wait quietly.
            AuthorizationRequired = true;
            Interlocked.Exchange(ref _accessToken, null);
            _accessTokenExpiresAt = DateTimeOffset.MinValue;
            try
            {
                // Clear the invalid token to prevent repeated failed calls to id.twitch.tv
                await tokenStore.StoreRefreshTokenAsync("twitch", string.Empty, cancellationToken);
            }
            catch
            {
                // ignore
            }
        }
    }

    private bool HasUsableAccessToken()
    {
        return !string.IsNullOrWhiteSpace(AccessToken)
            && _accessTokenExpiresAt > _timeProvider.GetUtcNow().Add(RefreshSkew);
    }

    private void StoreAccessToken(TwitchTokenResponse response)
    {
        Interlocked.Exchange(ref _accessToken, response.AccessToken);
        var expiresIn = Math.Max(0, response.ExpiresIn);
        _accessTokenExpiresAt = _timeProvider.GetUtcNow().AddSeconds(expiresIn);
    }
}

public interface ITwitchTokenEndpoint
{
    Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public sealed record TwitchTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn = 3600);
