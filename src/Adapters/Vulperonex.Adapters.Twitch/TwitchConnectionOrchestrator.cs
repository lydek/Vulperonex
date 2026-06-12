using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Adapters.Twitch.EventSub;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Adapters.Twitch.Reconnect;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Twitch;
using Vulperonex.Application.Settings;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch;

/// <summary>
/// Coordinates the live Twitch connection lifecycle (mirrors omni-commander
/// TwitchMonitorService): resolve the broadcaster from the channel login,
/// connect IRC chat + EventSub alerts, and retry on auth/connection failure.
/// Supervises the connection for the whole host lifetime: any
/// <see cref="PlatformConnectionChangedEvent"/> disconnect signal tears both
/// sources down and re-enters the connect loop with a fresh access token,
/// backing off via <see cref="ReconnectBackoffPolicy"/>.
/// </summary>
public sealed class TwitchConnectionOrchestrator(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    TwitchIrcChatSource ircSource,
    TwitchEventSubSource eventSubSource,
    IStreamEventBus eventBus,
    ILogger<TwitchConnectionOrchestrator> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private readonly ReconnectBackoffPolicy _backoffPolicy = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register Twitch event types regardless of connectivity.
        await using (var bootScope = serviceProvider.CreateAsyncScope())
        {
            var adapter = bootScope.ServiceProvider.GetRequiredService<TwitchAdapter>();
            await adapter.StartAsync(stoppingToken).ConfigureAwait(false);
        }

        if (!configuration.GetValue("Twitch:EventSub:Enabled", true))
        {
            logger.LogInformation("Twitch live ingestion disabled via Twitch:EventSub:Enabled.");
            return;
        }

        string? channelName;
        await using (var configScope = serviceProvider.CreateAsyncScope())
        {
            var settings = configScope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            channelName = await settings.GetAsync<string?>(SystemSettingKey.TwitchChannelName, null, stoppingToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            channelName = configuration["Twitch:ChannelName"];
        }
        channelName = channelName?.Trim();

        var configuredBroadcasterId = configuration["Twitch:BroadcasterId"]?.Trim();
        if (string.IsNullOrWhiteSpace(channelName) && string.IsNullOrWhiteSpace(configuredBroadcasterId))
        {
            logger.LogInformation("Skipping Twitch live ingestion: set Twitch:ChannelName or Twitch:BroadcasterId.");
            return;
        }

        // Both sources publish PlatformConnectionChangedEvent on disconnect
        // (including TwitchLib's own failed auto-reconnects, which surface as
        // auth_failed when the token has expired). Either source dropping
        // triggers a full teardown + reconnect of both, so subscriptions and
        // chat always come back together with a fresh token.
        var reconnectSignal = new SemaphoreSlim(0);
        using var disconnectSubscription = eventBus.Subscribe<PlatformConnectionChangedEvent>((changed, _) =>
        {
            if (string.Equals(changed.Platform, "twitch", StringComparison.OrdinalIgnoreCase) && !changed.IsConnected)
            {
                reconnectSignal.Release();
            }

            return Task.CompletedTask;
        });

        var attempt = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var connected = await TryConnectAsync(channelName, configuredBroadcasterId, stoppingToken).ConfigureAwait(false);
                if (!connected)
                {
                    var retryDelay = _backoffPolicy.GetDelay(++attempt);
                    await DelayQuietlyAsync(retryDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                attempt = 0;
                DrainSignals(reconnectSignal);

                // Park here while the connection is healthy; a disconnect from
                // either source releases the semaphore and restarts the loop.
                try
                {
                    await reconnectSignal.WaitAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var delay = _backoffPolicy.GetDelay(++attempt);
                logger.LogWarning("Twitch connection lost; reconnecting in {Delay:0.#}s.", delay.TotalSeconds);
                await DisconnectSourcesAsync().ConfigureAwait(false);
                await DelayQuietlyAsync(delay, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await DisconnectSourcesAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Runs one connect attempt: fresh token → broadcaster → IRC + EventSub.</summary>
    private async Task<bool> TryConnectAsync(
        string? channelName,
        string? configuredBroadcasterId,
        CancellationToken stoppingToken)
    {
        try
        {
            var tokenProvider = serviceProvider.GetRequiredService<TwitchAccessTokenProvider>();
            await tokenProvider.EnsureValidAccessTokenAsync(stoppingToken).ConfigureAwait(false);
            var accessToken = tokenProvider.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                logger.LogInformation("Waiting for Twitch OAuth authorization; retrying in {Delay}s.", RetryDelay.TotalSeconds);
                await DelayQuietlyAsync(RetryDelay, stoppingToken).ConfigureAwait(false);
                return false;
            }

            var broadcasterId = configuredBroadcasterId;
            var channelLogin = channelName;
            if (string.IsNullOrWhiteSpace(broadcasterId) || string.IsNullOrWhiteSpace(channelLogin))
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var helix = scope.ServiceProvider.GetRequiredService<IHelixClient>();
                var profile = await helix.LookupUserAsync(channelName, configuredBroadcasterId, stoppingToken).ConfigureAwait(false);
                if (profile is null)
                {
                    logger.LogWarning("Could not resolve Twitch channel '{Channel}'.", channelName ?? configuredBroadcasterId);
                    return false;
                }

                broadcasterId = profile.UserId;
                channelLogin = profile.Login;
            }

            logger.LogInformation("Starting Twitch live ingestion (channel: {Login}, broadcaster: {Id}).", channelLogin, broadcasterId);

            await ircSource.ConnectAsync(channelLogin!, accessToken, stoppingToken).ConfigureAwait(false);
            await eventSubSource.ConnectAsync(broadcasterId!, stoppingToken).ConfigureAwait(false);

            logger.LogInformation("Twitch live ingestion started.");
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Twitch live ingestion connect attempt failed.");
            return false;
        }
    }

    private async Task DisconnectSourcesAsync()
    {
        try
        {
            await ircSource.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to disconnect Twitch IRC source.");
        }

        try
        {
            await eventSubSource.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to disconnect Twitch EventSub source.");
        }
    }

    private static void DrainSignals(SemaphoreSlim signal)
    {
        while (signal.CurrentCount > 0 && signal.Wait(0))
        {
        }
    }

    private static async Task DelayQuietlyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
