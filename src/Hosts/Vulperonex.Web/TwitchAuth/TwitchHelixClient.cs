using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchHelixClient(
    IConfiguration configuration,
    TwitchAccessTokenProvider accessTokenProvider,
    ISystemSettingsService settings,
    HttpClient? httpClient = null) : IHelixClient
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient
    {
        BaseAddress = new Uri("https://api.twitch.tv/helix/"),
    };

    public async Task<PlatformUserProfile?> LookupUserAsync(
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
            : new PlatformUserProfile(
                user.Id,
                user.Login,
                user.DisplayName,
                user.ProfileImageUrl,
                user.Description,
                user.BroadcasterType is "affiliate" or "partner");
    }

    public async Task<PlatformShoutoutResult> SendShoutoutAsync(
        string targetLogin,
        CancellationToken cancellationToken = default)
    {
        var target = await LookupUserAsync(targetLogin, userId: null, cancellationToken);
        if (target is null)
        {
            return new PlatformShoutoutResult(false, targetLogin, null, null);
        }

        var broadcasterId = configuration["Twitch:BroadcasterId"]
            ?? throw new InvalidOperationException("Twitch:BroadcasterId is required for shoutout actions.");
        var moderatorId = configuration["Twitch:ModeratorId"] ?? broadcasterId;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"chat/shoutouts?from_broadcaster_id={Uri.EscapeDataString(broadcasterId)}&to_broadcaster_id={Uri.EscapeDataString(target.UserId)}&moderator_id={Uri.EscapeDataString(moderatorId)}");
        await AddHelixHeadersAsync(request, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new PlatformShoutoutResult(true, target.Login, target.UserId, target.DisplayName);
    }

    public async Task<IReadOnlyList<PlatformBadgeDescriptor>> GetGlobalBadgesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "chat/badges/global");
        await AddHelixHeadersAsync(request, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TwitchBadgesResponse>(cancellationToken);
        return MapBadges(payload, isChannel: false);
    }

    public async Task<IReadOnlyList<PlatformBadgeDescriptor>> GetChannelBadgesAsync(
        string broadcasterId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentException("Broadcaster id is required.", nameof(broadcasterId));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"chat/badges?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
        await AddHelixHeadersAsync(request, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TwitchBadgesResponse>(cancellationToken);
        return MapBadges(payload, isChannel: true);
    }

    private static IReadOnlyList<PlatformBadgeDescriptor> MapBadges(TwitchBadgesResponse? payload, bool isChannel)
    {
        if (payload?.Data is null || payload.Data.Count == 0)
        {
            return [];
        }

        var result = new List<PlatformBadgeDescriptor>();
        foreach (var set in payload.Data)
        {
            if (set.Versions is null) continue;
            foreach (var version in set.Versions)
            {
                result.Add(new PlatformBadgeDescriptor(
                    Key: $"{set.SetId}_{version.Id}",
                    SetId: set.SetId,
                    Version: version.Id,
                    ImageUrl1x: version.ImageUrl1x ?? string.Empty,
                    Title: version.Title,
                    Description: version.Description,
                    IsChannel: isChannel));
            }
        }

        return result;
    }

    public async Task<bool> RefundRedemptionAsync(
        string rewardId,
        string redemptionId,
        CancellationToken cancellationToken = default)
    {
        var broadcasterId = configuration["Twitch:BroadcasterId"]
            ?? throw new InvalidOperationException("Twitch:BroadcasterId is required for redemption refund actions.");
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"channel_points/custom_rewards/redemptions?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&reward_id={Uri.EscapeDataString(rewardId)}&id={Uri.EscapeDataString(redemptionId)}")
        {
            Content = JsonContent.Create(new { status = "CANCELED" }),
        };
        await AddHelixHeadersAsync(request, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return true;
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
        var dbClientId = await settings.GetAsync<string?>(SystemSettingKey.TwitchClientId, null, cancellationToken);
        var clientId = !string.IsNullOrWhiteSpace(dbClientId) ? dbClientId : configuration["Twitch:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Twitch:ClientId is required.");
        }
        request.Headers.Add("Client-Id", clientId);
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

    private sealed record TwitchBadgesResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<TwitchBadgeSetJson> Data);

    private sealed record TwitchBadgeSetJson(
        [property: JsonPropertyName("set_id")] string SetId,
        [property: JsonPropertyName("versions")] IReadOnlyList<TwitchBadgeVersionJson> Versions);

    private sealed record TwitchBadgeVersionJson(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("image_url_1x")] string? ImageUrl1x,
        [property: JsonPropertyName("image_url_2x")] string? ImageUrl2x,
        [property: JsonPropertyName("image_url_4x")] string? ImageUrl4x,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("click_action")] string? ClickAction,
        [property: JsonPropertyName("click_url")] string? ClickUrl);
}
