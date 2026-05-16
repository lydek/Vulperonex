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

        var response = await tokenEndpoint.RefreshAsync(refreshToken, cancellationToken);
        Interlocked.Exchange(ref _accessToken, response.AccessToken);
        await tokenStore.StoreRefreshTokenAsync("twitch", response.RefreshToken, cancellationToken);
    }
}

public interface ITwitchTokenEndpoint
{
    Task<TwitchTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public sealed record TwitchTokenResponse(string AccessToken, string RefreshToken);
