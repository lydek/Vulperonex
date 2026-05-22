namespace Vulperonex.Application.Twitch;

public interface ITwitchHelixClient
{
    Task<TwitchHelixUser?> LookupUserAsync(
        string? login,
        string? userId,
        CancellationToken cancellationToken = default);
}

public sealed record TwitchHelixUser(
    string UserId,
    string Login,
    string DisplayName,
    string? Avatar,
    string? Description,
    bool IsAffiliate);
