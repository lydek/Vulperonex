using Vulperonex.Application.Auth;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class TwitchAccessTokenProvider(IOAuthTokenStore tokenStore, ITwitchTokenEndpoint tokenEndpoint)
{
    private string? _accessToken;

    public string? AccessToken => Volatile.Read(ref _accessToken);

    public bool AuthorizationRequired { get; private set; }

    public async Task ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var response = await tokenEndpoint.ExchangeCodeAsync(code, codeVerifier, cancellationToken);
        Interlocked.Exchange(ref _accessToken, response.AccessToken);
        await tokenStore.StoreRefreshTokenAsync("twitch", response.RefreshToken, cancellationToken);
    }

    public async Task RefreshOnStartupAsync(CancellationToken cancellationToken = default)
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
            Interlocked.Exchange(ref _accessToken, response.AccessToken);
            await tokenStore.StoreRefreshTokenAsync("twitch", response.RefreshToken, cancellationToken);
        }
        catch (Exception)
        {
            // If the refresh token is expired, revoked or invalid, set AuthorizationRequired = true
            // and gracefully set accessToken to null so orchestrators can wait quietly.
            AuthorizationRequired = true;
            Interlocked.Exchange(ref _accessToken, null);
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
}

public interface ITwitchTokenEndpoint
{
    Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public sealed record TwitchTokenResponse(string AccessToken, string RefreshToken);
