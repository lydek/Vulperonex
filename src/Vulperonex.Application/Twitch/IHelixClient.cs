namespace Vulperonex.Application.Twitch;

public interface IHelixClient
{
    Task<PlatformUserProfile?> LookupUserAsync(
        string? login,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search channels by a display-name query and return the single channel whose display name
    /// or login matches the query exactly (case-insensitive), or null. Used to resolve a display
    /// name that is not already known locally — including non-ASCII names that the users endpoint
    /// (login-only) cannot resolve.
    /// </summary>
    Task<PlatformUserProfile?> SearchChannelExactAsync(
        string query,
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

    Task<IReadOnlyList<PlatformRewardDescriptor>> GetCustomRewardsAsync(
        string broadcasterId,
        CancellationToken cancellationToken = default);
}

public sealed record PlatformRewardDescriptor(
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
