using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Adapters.Twitch.Helix;

public sealed class TwitchBadgeSyncCoordinator(
    IPlatformBadgeCache cache,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<TwitchBadgeSyncCoordinator> logger)
{
    private int _syncQueuedOrRunning;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var acquired = false;
        try
        {
            await _syncGate.WaitAsync(cancellationToken);
            acquired = true;
            await cache.SyncGlobalAsync(cancellationToken);

            var broadcasterId = configuration["Twitch:BroadcasterId"];
            if (string.IsNullOrWhiteSpace(broadcasterId))
            {
                logger.LogInformation("Skipping Twitch channel badge sync: Twitch:BroadcasterId not configured.");
                return;
            }

            await cache.SyncChannelAsync(broadcasterId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Twitch badge sync failed.");
        }
        finally
        {
            if (acquired)
            {
                _syncGate.Release();
            }
        }
    }

    public void QueueSync()
    {
        if (Interlocked.CompareExchange(ref _syncQueuedOrRunning, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await SyncAsync(lifetime.ApplicationStopping);
            }
            finally
            {
                Interlocked.Exchange(ref _syncQueuedOrRunning, 0);
            }
        }, CancellationToken.None);
    }
}
