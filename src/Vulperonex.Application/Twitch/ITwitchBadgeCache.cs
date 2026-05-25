namespace Vulperonex.Application.Twitch;

public interface ITwitchBadgeCache
{
    bool IsReady { get; }

    string? GetUrl(string key);

    TwitchBadgeDescriptor? Get(string key);

    IReadOnlyList<TwitchBadgeDescriptor> ListGlobal();

    IReadOnlyList<TwitchBadgeDescriptor> ListChannel();

    Task SyncGlobalAsync(CancellationToken cancellationToken = default);

    Task SyncChannelAsync(string broadcasterId, CancellationToken cancellationToken = default);
}
