using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vulperonex.Adapters.Twitch.Auth;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchTokenEndpoint(IConfiguration configuration) : ITwitchTokenEndpoint
{
    private static readonly Uri TokenEndpoint = new("https://id.twitch.tv/oauth2/token");
    private static readonly Uri DeviceEndpoint = new("https://id.twitch.tv/oauth2/device");

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
                ["client_secret"] = GetClientSecret(),
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            },
            cancellationToken);
    }

    public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            };
        var clientSecret = configuration["Twitch:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            form["client_secret"] = clientSecret;
        }

        return SendTokenRequestAsync(form, cancellationToken);
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
                ["client_secret"] = GetClientSecret(),
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            },
            cancellationToken);
    }

    public async Task<TwitchDeviceAuthorizationResponse> StartDeviceAuthorizationAsync(
        string scopes,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync(
            DeviceEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["scopes"] = scopes,
            }),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new TwitchTokenExchangeException(response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TwitchDeviceAuthorizationJson>(cancellationToken);
        return new TwitchDeviceAuthorizationResponse(
            payload!.DeviceCode,
            payload.UserCode,
            payload.VerificationUri,
            payload.ExpiresIn,
            payload.Interval);
    }

    public Task<TwitchTokenResponse> CompleteDeviceAuthorizationAsync(
        string deviceCode,
        CancellationToken cancellationToken = default)
    {
        return SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = GetClientId(),
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            },
            cancellationToken);
    }

    private async Task<TwitchTokenResponse> SendTokenRequestAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<TwitchErrorJson>(cancellationToken);
            if (string.Equals(error?.Message, "authorization_pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(error?.Error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new TwitchDeviceAuthorizationPendingException();
            }

            throw new TwitchTokenExchangeException(response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<TwitchTokenJson>(cancellationToken);
        return new TwitchTokenResponse(token!.AccessToken, token.RefreshToken);
    }

    private string GetClientId()
    {
        return configuration["Twitch:ClientId"]
            ?? throw new InvalidOperationException("Twitch:ClientId is required.");
    }

    private string GetClientSecret()
    {
        return configuration["Twitch:ClientSecret"]
            ?? throw new TwitchClientSecretMissingException();
    }

    private sealed record TwitchTokenJson(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);

    private sealed record TwitchDeviceAuthorizationJson(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval);

    private sealed record TwitchErrorJson(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("message")] string? Message);
}

public sealed record TwitchDeviceAuthorizationResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn,
    int Interval);
