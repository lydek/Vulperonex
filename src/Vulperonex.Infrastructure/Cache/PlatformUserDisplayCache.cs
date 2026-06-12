using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Cache;

public sealed class PlatformUserDisplayCache : IPlatformUserInfoCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LruCache<string, PlatformUserDisplayInfo> _l1;
    private readonly VulperonexDbContext? _context;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>Binds the cache to one DbContext for its lifetime (scoped usage and tests).</summary>
    public PlatformUserDisplayCache(VulperonexDbContext context, int l1Capacity = 500)
    {
        _context = context;
        L1Capacity = l1Capacity;
        _l1 = new LruCache<string, PlatformUserDisplayInfo>(l1Capacity);
    }

    /// <summary>
    /// Process-lifetime mode: the L1 LRU survives across events and a short-lived
    /// scope is opened per database access. This is the production registration —
    /// a scoped cache is rebuilt for every event scope, so its L1 never hits.
    /// </summary>
    public PlatformUserDisplayCache(IServiceScopeFactory scopeFactory, int l1Capacity = 500)
    {
        _scopeFactory = scopeFactory;
        L1Capacity = l1Capacity;
        _l1 = new LruCache<string, PlatformUserDisplayInfo>(l1Capacity);
    }

    public int L1Capacity { get; }

    public TimeSpan Ttl { get; private init; } = TimeSpan.FromHours(24);

    public static async Task<PlatformUserDisplayCache> CreateAsync(
        VulperonexDbContext context,
        ISystemSettingsService settings,
        CancellationToken cancellationToken = default)
    {
        var capacity = await settings.GetAsync(SystemSettingKey.OverlayDisplayCacheL1Capacity, 500, cancellationToken);
        var ttlHours = await settings.GetAsync(SystemSettingKey.OverlayDisplayCacheTtlHours, 24, cancellationToken);
        return new PlatformUserDisplayCache(context, capacity)
        {
            Ttl = TimeSpan.FromHours(ttlHours),
        };
    }

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

        var displayInfo = await WithContextAsync(async context =>
        {
            var row = await context.PlatformUserDisplayInfo
                .FindAsync([platform, platformUserId], cancellationToken)
                .ConfigureAwait(false);
            return row is null ? null : FromEntity(row);
        }).ConfigureAwait(false);

        if (displayInfo is null)
        {
            return null;
        }

        _l1.Set(key, displayInfo);
        return displayInfo;
    }

    public async Task<PlatformUserDisplayInfo> UpdateAsync(
        string platform,
        string platformUserId,
        Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
        CancellationToken cancellationToken = default)
    {
        var updated = await WithContextAsync(async context =>
        {
            var row = await context.PlatformUserDisplayInfo
                .FindAsync([platform, platformUserId], cancellationToken)
                .ConfigureAwait(false);
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

            var next = updater(FromEntity(row));
            Apply(row, next);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return next;
        }).ConfigureAwait(false);

        _l1.Set(CacheKey(platform, platformUserId), updated);
        return updated;
    }

    private async Task<T> WithContextAsync<T>(Func<VulperonexDbContext, Task<T>> action)
    {
        if (_scopeFactory is null)
        {
            return await action(_context!).ConfigureAwait(false);
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<VulperonexDbContext>()).ConfigureAwait(false);
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
            row.FetchedAt,
            Login: row.Login);
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
        row.Login = displayInfo.Login;
    }

    private static string CacheKey(string platform, string platformUserId)
    {
        return $"{platform}:{platformUserId}";
    }
}
