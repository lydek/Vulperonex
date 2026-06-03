using System.Net;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

/// <summary>
/// In-memory snapshot of the broadcaster's Twitch channel-point custom rewards.
/// The trigger editor UI consumes this list to offer autocomplete + a manual
/// refresh button so newly created rewards become visible without restarting
/// the host. Mirrors <see cref="TwitchBadgeCache"/> in shape.
/// </summary>
public interface ITwitchRewardCache
{
    bool IsReady { get; }
    DateTimeOffset? LastRefreshedAt { get; }
    IReadOnlyList<PlatformRewardDescriptor> List();
    Task RefreshAsync(CancellationToken cancellationToken = default);
    void QueueRefresh();
}

public sealed class TwitchRewardCache(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<TwitchRewardCache> logger) : ITwitchRewardCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<PlatformRewardDescriptor> _rewards = [];
    private DateTimeOffset? _lastRefreshedAt;
    private int _refreshQueuedOrRunning;

    public bool IsReady => _lastRefreshedAt is not null;

    public DateTimeOffset? LastRefreshedAt => _lastRefreshedAt;

    public IReadOnlyList<PlatformRewardDescriptor> List() => _rewards;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var acquired = false;
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            await using var scope = scopeFactory.CreateAsyncScope();
            var helix = scope.ServiceProvider.GetRequiredService<IHelixClient>();
            var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

            var broadcasterId = await ResolveBroadcasterIdAsync(scope.ServiceProvider, settings, helix, cancellationToken).ConfigureAwait(false);
            if (broadcasterId is null)
            {
                logger.LogInformation("Skipping Twitch reward refresh: broadcaster could not be resolved.");
                return;
            }

            var rewards = await helix.GetCustomRewardsAsync(broadcasterId, cancellationToken).ConfigureAwait(false);
            _rewards = rewards;
            _lastRefreshedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Loaded {Count} Twitch channel-point rewards.", rewards.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning(ex, "Twitch reward refresh skipped: authorization required ({Status}).", ex.StatusCode);
        }
        catch (InvalidOperationException ex)
        {
            // Raised by TwitchHelixClient when OAuth/Client-Id not present.
            logger.LogWarning(ex, "Twitch reward refresh skipped: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Twitch reward refresh failed.");
        }
        finally
        {
            if (acquired)
            {
                _gate.Release();
            }
        }
    }

    public void QueueRefresh()
    {
        if (Interlocked.CompareExchange(ref _refreshQueuedOrRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(lifetime.ApplicationStopping).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _refreshQueuedOrRunning, 0);
            }
        }, CancellationToken.None);
    }

    private async Task<string?> ResolveBroadcasterIdAsync(
        IServiceProvider services,
        ISystemSettingsService settings,
        IHelixClient helix,
        CancellationToken cancellationToken)
    {
        var configuredBroadcasterId = configuration["Twitch:BroadcasterId"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredBroadcasterId))
        {
            return configuredBroadcasterId;
        }

        var channelName = await settings
            .GetAsync<string?>(SystemSettingKey.TwitchChannelName, null, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(channelName))
        {
            channelName = configuration["Twitch:ChannelName"];
        }

        channelName = channelName?.Trim();
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return null;
        }

        var profile = await helix.LookupUserAsync(channelName, null, cancellationToken).ConfigureAwait(false);
        return profile?.UserId;
    }
}
