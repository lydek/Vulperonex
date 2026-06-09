namespace Vulperonex.Application.Members;

public interface IPlatformUserDisplayInfoProvider
{
    Task<PlatformUserDisplayInfo?> GetAsync(
        string platform,
        string platformUserId,
        CancellationToken cancellationToken = default);
}

public sealed record PlatformUserDisplayInfo(
    string DisplayName,
    string? AvatarUrl,
    bool IsSubscriber,
    string? Login = null);
