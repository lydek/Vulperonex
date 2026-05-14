namespace Vulperonex.Adapters.Abstractions;

public interface IPlatformUserInfoCache
{
    Task<PlatformUserDisplayInfo?> GetAsync(
        string platform,
        string platformUserId,
        CancellationToken cancellationToken = default);

    Task<PlatformUserDisplayInfo> UpdateAsync(
        string platform,
        string platformUserId,
        Func<PlatformUserDisplayInfo, PlatformUserDisplayInfo> updater,
        CancellationToken cancellationToken = default);
}

public sealed record PlatformUserDisplayInfo(
    string Platform,
    string PlatformUserId,
    string? DisplayName,
    string? AvatarUrl,
    string? ColorHex,
    IReadOnlyCollection<string> Badges,
    bool IsSubscriber,
    string? SubscriptionTier,
    long TotalBitsGiven,
    DateTimeOffset FetchedAt);
