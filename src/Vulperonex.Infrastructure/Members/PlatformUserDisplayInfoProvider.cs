using Vulperonex.Adapters.Abstractions;
using Vulperonex.Application.Members;

namespace Vulperonex.Infrastructure.Members;

public sealed class PlatformUserDisplayInfoProvider(IPlatformUserInfoCache userInfoCache) : IPlatformUserDisplayInfoProvider
{
    public async Task<Vulperonex.Application.Members.PlatformUserDisplayInfo?> GetAsync(
        string platform,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        var displayInfo = await userInfoCache.GetAsync(platform, platformUserId, cancellationToken);
        if (displayInfo is null)
        {
            return null;
        }

        return new Vulperonex.Application.Members.PlatformUserDisplayInfo(
            displayInfo.DisplayName ?? platformUserId,
            displayInfo.AvatarUrl,
            displayInfo.IsSubscriber);
    }
}
