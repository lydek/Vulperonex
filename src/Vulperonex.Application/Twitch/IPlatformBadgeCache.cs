namespace Vulperonex.Application.Twitch;

public interface IPlatformBadgeCache
{
    bool IsReady { get; }

    string? GetUrl(string key);

    PlatformBadgeDescriptor? Get(string key);

    IReadOnlyList<PlatformBadgeDescriptor> ListGlobal();

    IReadOnlyList<PlatformBadgeDescriptor> ListChannel();

    Task SyncGlobalAsync(CancellationToken cancellationToken = default);

    Task SyncChannelAsync(string broadcasterId, CancellationToken cancellationToken = default);
}
