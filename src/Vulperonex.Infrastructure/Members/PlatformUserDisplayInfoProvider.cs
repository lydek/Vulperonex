using Microsoft.Extensions.Logging;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Infrastructure.Members;

public sealed class PlatformUserDisplayInfoProvider(
    IPlatformUserInfoCache userInfoCache,
    IHelixClient helixClient,
    ILogger<PlatformUserDisplayInfoProvider> logger) : IPlatformUserDisplayInfoProvider
{
    public async Task<Vulperonex.Application.Members.PlatformUserDisplayInfo?> GetAsync(
        string platform,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        var displayInfo = await userInfoCache.GetAsync(platform, platformUserId, cancellationToken);
        
        string? avatarUrl = displayInfo?.AvatarUrl;
        string? displayName = displayInfo?.DisplayName;
        bool isSubscriber = displayInfo?.IsSubscriber ?? false;
        string? login = displayInfo?.Login;

        if (string.Equals(platform, "twitch", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(avatarUrl))
        {
            try
            {
                // Defensive lookup: check if platformUserId is a numeric Twitch UserId or a login string
                bool isNumericId = false;
                if (long.TryParse(platformUserId, out var parsedId))
                {
                    isNumericId = parsedId > 0;
                }

                var profile = isNumericId
                    ? await helixClient.LookupUserAsync(login: null, userId: platformUserId, cancellationToken).ConfigureAwait(false)
                    : await helixClient.LookupUserAsync(login: platformUserId, userId: null, cancellationToken).ConfigureAwait(false);

                if (profile != null)
                {
                    avatarUrl = profile.Avatar;
                    displayName = profile.DisplayName;

                    await userInfoCache.UpdateAsync(platform, platformUserId, current => current with
                    {
                        DisplayName = profile.DisplayName,
                        AvatarUrl = profile.Avatar,
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to auto-fetch Twitch avatar for user {UserId} in PlatformUserDisplayInfoProvider.", platformUserId);
            }
        }

        if (displayInfo is null && string.IsNullOrWhiteSpace(avatarUrl))
        {
            return null;
        }

        return new Vulperonex.Application.Members.PlatformUserDisplayInfo(
            displayName ?? platformUserId,
            avatarUrl,
            isSubscriber,
            login);
    }
}
