using Microsoft.Extensions.Logging;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchBadgeSyncHostedService(
    ITwitchBadgeCache cache,
    IConfiguration configuration,
    ILogger<TwitchBadgeSyncHostedService> logger) : IHostedService
{
    private CancellationTokenSource? _cts;
    private Task? _syncTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _syncTask = Task.Run(() => SyncAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_syncTask is not null)
        {
            try
            {
                await _syncTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        try
        {
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
            logger.LogWarning(ex, "Twitch badge initial sync failed.");
        }
    }
}
