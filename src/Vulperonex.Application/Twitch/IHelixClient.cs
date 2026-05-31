namespace Vulperonex.Application.Twitch;

public interface IHelixClient
{
    Task<PlatformUserProfile?> LookupUserAsync(
        string? login,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<PlatformShoutoutResult> SendShoutoutAsync(
        string targetLogin,
        CancellationToken cancellationToken = default);

    Task<bool> RefundRedemptionAsync(
        string rewardId,
        string redemptionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformBadgeDescriptor>> GetGlobalBadgesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformBadgeDescriptor>> GetChannelBadgesAsync(
        string broadcasterId,
        CancellationToken cancellationToken = default);

    Task CreateEventSubSubscriptionAsync(
        string type,
        string version,
        IReadOnlyDictionary<string, string> condition,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TwitchRewardDescriptor>> GetCustomRewardsAsync(
        string broadcasterId,
        CancellationToken cancellationToken = default);
}

public sealed record TwitchRewardDescriptor(
    string Id,
    string Title,
    int Cost,
    bool IsEnabled,
    string? ImageUrl);

public sealed record PlatformUserProfile(
    string UserId,
    string Login,
    string DisplayName,
    string? Avatar,
    string? Description,
    bool IsAffiliate);

public sealed record PlatformShoutoutResult(
    bool IsSent,
    string TargetLogin,
    string? TargetUserId,
    string? TargetDisplayName);
