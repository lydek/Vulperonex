using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchHelixClient(
    IConfiguration configuration,
    TwitchAccessTokenProvider accessTokenProvider,
    HttpClient? httpClient = null) : ITwitchHelixClient
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        BaseAddress = new Uri("https://api.twitch.tv/helix/"),
    };

    public async Task<TwitchHelixUser?> LookupUserAsync(
        string? login,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var query = BuildUserQuery(login, userId);
        if (query is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"users?{query}");
        await AddHelixHeadersAsync(request, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TwitchUsersResponse>(cancellationToken);
        var user = payload?.Data.FirstOrDefault();
        return user is null
            ? null
            : new TwitchHelixUser(
                user.Id,
                user.Login,
                user.DisplayName,
                user.ProfileImageUrl,
                user.Description,
                user.BroadcasterType is "affiliate" or "partner");
    }

    private async Task AddHelixHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessTokenProvider.AccessToken))
        {
            await accessTokenProvider.RefreshOnStartupAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(accessTokenProvider.AccessToken))
        {
            throw new InvalidOperationException("Twitch OAuth authorization is required.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenProvider.AccessToken);
        request.Headers.Add("Client-Id", configuration["Twitch:ClientId"]
            ?? throw new InvalidOperationException("Twitch:ClientId is required."));
    }

    private static string? BuildUserQuery(string? login, string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"id={Uri.EscapeDataString(userId)}";
        }

        return string.IsNullOrWhiteSpace(login)
            ? null
            : $"login={Uri.EscapeDataString(login)}";
    }

    private sealed record TwitchUsersResponse(IReadOnlyList<TwitchUserJson> Data);

    private sealed record TwitchUserJson(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("profile_image_url")] string? ProfileImageUrl,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("broadcaster_type")] string? BroadcasterType);
}
