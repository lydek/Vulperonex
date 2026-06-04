using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchBadgeCache(
    IServiceScopeFactory scopeFactory,
    ILogger<TwitchBadgeCache> logger) : IPlatformBadgeCache
{
    private readonly ConcurrentDictionary<string, PlatformBadgeDescriptor> _global = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PlatformBadgeDescriptor> _channel = new(StringComparer.Ordinal);
    private int _globalSynced;
    private int _channelSynced;

    public bool IsReady => Volatile.Read(ref _globalSynced) == 1;

    public string? GetUrl(string key)
    {
        var descriptor = Get(key);
        return descriptor is null || string.IsNullOrWhiteSpace(descriptor.ImageUrl1x)
            ? null
            : descriptor.ImageUrl1x;
    }

    public PlatformBadgeDescriptor? Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var normalized = NormalizeKey(key);
        if (_channel.TryGetValue(normalized, out var channelHit)) return channelHit;
        if (_global.TryGetValue(normalized, out var globalHit)) return globalHit;
        return null;
    }

    public IReadOnlyList<PlatformBadgeDescriptor> ListGlobal() => _global.Values.ToArray();

    public IReadOnlyList<PlatformBadgeDescriptor> ListChannel() => _channel.Values.ToArray();

    public async Task SyncGlobalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IHelixClient>();
            var badges = await client.GetGlobalBadgesAsync(cancellationToken);
            Replace(_global, badges);
            Interlocked.Exchange(ref _globalSynced, 1);
            logger.LogInformation("Synced {Count} global Twitch badges.", badges.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning("Twitch global badge sync skipped: authorization required ({Status}).", ex.StatusCode);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Twitch global badge sync skipped: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync Twitch global badges.");
        }
    }

    public async Task SyncChannelAsync(string broadcasterId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<IHelixClient>();
            var badges = await client.GetChannelBadgesAsync(broadcasterId, cancellationToken);
            Replace(_channel, badges);
            Interlocked.Exchange(ref _channelSynced, 1);
            logger.LogInformation("Synced {Count} channel Twitch badges for {BroadcasterId}.", badges.Count, broadcasterId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning("Twitch channel badge sync skipped for {BroadcasterId}: authorization required ({Status}).", broadcasterId, ex.StatusCode);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Twitch channel badge sync skipped for {BroadcasterId}: {Message}", broadcasterId, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync Twitch channel badges for {BroadcasterId}.", broadcasterId);
        }
    }

    private static void Replace(ConcurrentDictionary<string, PlatformBadgeDescriptor> target, IReadOnlyList<PlatformBadgeDescriptor> source)
    {
        target.Clear();
        foreach (var descriptor in source)
        {
            target[descriptor.Key] = descriptor;
        }
    }

    private static string NormalizeKey(string key)
    {
        // Accept both Twitch IRC tag form (subscriber/0) and storage form (subscriber_0).
        return key.Replace('/', '_');
    }
}
