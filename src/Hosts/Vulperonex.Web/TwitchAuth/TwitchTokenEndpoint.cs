using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vulperonex.Adapters.Twitch.Auth;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchTokenEndpoint(IConfiguration configuration) : ITwitchTokenEndpoint
{
    private static readonly Uri TokenEndpoint = new("https://id.twitch.tv/oauth2/token");

    public Task<TwitchTokenResponse> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var clientId = GetClientId();
        var redirectUri = configuration["Twitch:RedirectUri"]
            ?? throw new InvalidOperationException("Twitch redirect URI must be supplied by the OAuth session.");
        return SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            },
            cancellationToken);
    }

    public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            cancellationToken);
    }

    public async Task<TwitchTokenResponse> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        return await SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            },
            cancellationToken);
    }

    private async Task<TwitchTokenResponse> SendTokenRequestAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TwitchTokenJson>(cancellationToken);
        return new TwitchTokenResponse(token!.AccessToken, token.RefreshToken);
    }

    private string GetClientId()
    {
        return configuration["Twitch:ClientId"]
            ?? throw new InvalidOperationException("Twitch:ClientId is required.");
    }

    private sealed record TwitchTokenJson(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);
}
