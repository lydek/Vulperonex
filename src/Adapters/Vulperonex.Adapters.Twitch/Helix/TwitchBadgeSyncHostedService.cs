using Microsoft.Extensions.Hosting;
namespace Vulperonex.Adapters.Twitch.Helix;

public sealed class TwitchBadgeSyncHostedService(
    TwitchBadgeSyncCoordinator coordinator) : IHostedService
{
    private CancellationTokenSource? _cts;
    private Task? _syncTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _syncTask = Task.Run(() => coordinator.SyncAsync(_cts.Token), CancellationToken.None);
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
}
