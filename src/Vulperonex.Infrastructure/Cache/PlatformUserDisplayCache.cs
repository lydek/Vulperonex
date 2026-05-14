using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Cache;

public sealed class PlatformUserDisplayCache(
    VulperonexDbContext context,
    int l1Capacity = 500) : IPlatformUserInfoCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LruCache<string, PlatformUserDisplayInfo> _l1 = new(l1Capacity);

    public async Task<PlatformUserDisplayInfo?> GetAsync(
        string platform,
        string platformUserId,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(platform, platformUserId);
        if (_l1.TryGet(key, out var cached))
        {
            return cached;
        }

        var row = await context.PlatformUserDisplayInfo.FindAsync([platform, platformUserId], cancellationToken);
        if (row is null)
        {
            return null;
        }

        var displayInfo = FromEntity(row);
        _l1.Set(key, displayInfo);
        return displayInfo;
    }

    public async Task<PlatformUserDisplayInfo> UpdateAsync(
        string platform,
        string platformUserId,
        Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
        CancellationToken cancellationToken = default)
    {
        var row = await context.PlatformUserDisplayInfo.FindAsync([platform, platformUserId], cancellationToken);
        if (row is null)
        {
            row = new PlatformUserDisplayInfoEntity
            {
                Platform = platform,
                PlatformUserId = platformUserId,
                BadgesJson = "[]",
                FetchedAt = DateTimeOffset.UtcNow,
            };
            context.PlatformUserDisplayInfo.Add(row);
        }

        var updated = updater(FromEntity(row));
        Apply(row, updated);
        await context.SaveChangesAsync(cancellationToken);
        _l1.Set(CacheKey(platform, platformUserId), updated);
        return updated;
    }

    private static PlatformUserDisplayInfo FromEntity(PlatformUserDisplayInfoEntity row)
    {
        return new PlatformUserDisplayInfo(
            row.Platform,
            row.PlatformUserId,
            row.DisplayName,
            row.AvatarUrl,
            row.ColorHex,
            JsonSerializer.Deserialize<string[]>(row.BadgesJson, JsonOptions) ?? [],
            row.IsSubscriber,
            row.SubscriptionTier,
            row.TotalBitsGiven,
            row.FetchedAt);
    }

    private static void Apply(PlatformUserDisplayInfoEntity row, PlatformUserDisplayInfo displayInfo)
    {
        row.DisplayName = displayInfo.DisplayName;
        row.AvatarUrl = displayInfo.AvatarUrl;
        row.ColorHex = displayInfo.ColorHex;
        row.BadgesJson = JsonSerializer.Serialize(displayInfo.Badges, JsonOptions);
        row.IsSubscriber = displayInfo.IsSubscriber;
        row.SubscriptionTier = displayInfo.SubscriptionTier;
        row.TotalBitsGiven = displayInfo.TotalBitsGiven;
        row.FetchedAt = displayInfo.FetchedAt == default ? DateTimeOffset.UtcNow : displayInfo.FetchedAt;
    }

    private static string CacheKey(string platform, string platformUserId)
    {
        return $"{platform}:{platformUserId}";
    }
}
