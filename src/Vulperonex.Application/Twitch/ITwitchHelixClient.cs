namespace Vulperonex.Application.Twitch;

public interface ITwitchHelixClient
{
    Task<TwitchHelixUser?> LookupUserAsync(
        string? login,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<TwitchShoutoutResult> SendShoutoutAsync(
        string targetLogin,
        CancellationToken cancellationToken = default);

    Task<bool> RefundRedemptionAsync(
        string rewardId,
        string redemptionId,
        CancellationToken cancellationToken = default);
}

public sealed record TwitchHelixUser(
    string UserId,
    string Login,
    string DisplayName,
    string? Avatar,
    string? Description,
    bool IsAffiliate);

public sealed record TwitchShoutoutResult(
    bool IsSent,
    string TargetLogin,
    string? TargetUserId,
    string? TargetDisplayName);
