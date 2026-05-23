namespace Vulperonex.Application.Twitch;

public interface ITwitchHelixClient
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
}

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
