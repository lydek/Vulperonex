using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Twitch;
using Vulperonex.Application.Settings;

namespace Vulperonex.Web.TwitchAuth;

/// <summary>
/// Coordinates the live Twitch connection lifecycle (mirrors omni-commander
/// TwitchMonitorService): resolve the broadcaster from the channel login,
/// connect IRC chat + EventSub alerts, and retry on auth/connection failure.
/// </summary>
public sealed class TwitchConnectionOrchestrator(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    TwitchIrcChatSource ircSource,
    TwitchEventSubSource eventSubSource,
    ILogger<TwitchConnectionOrchestrator> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var tokenProvider = scope.ServiceProvider.GetRequiredService<TwitchAccessTokenProvider>();
                await tokenProvider.RefreshOnStartupAsync(stoppingToken).ConfigureAwait(false);
                var accessToken = tokenProvider.AccessToken;
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    logger.LogInformation("Waiting for Twitch OAuth authorization; retrying in {Delay}s.", RetryDelay.TotalSeconds);
                    await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var broadcasterId = configuredBroadcasterId;
                var channelLogin = channelName;
                if (string.IsNullOrWhiteSpace(broadcasterId) || string.IsNullOrWhiteSpace(channelLogin))
                {
                    var helix = scope.ServiceProvider.GetRequiredService<IHelixClient>();
                    var profile = await helix.LookupUserAsync(channelName, configuredBroadcasterId, stoppingToken).ConfigureAwait(false);
                    if (profile is null)
                    {
                        logger.LogWarning("Could not resolve Twitch channel '{Channel}'; retrying in {Delay}s.", channelName ?? configuredBroadcasterId, RetryDelay.TotalSeconds);
                        await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    broadcasterId = profile.UserId;
                    channelLogin = profile.Login;
                }

                logger.LogInformation("Starting Twitch live ingestion (channel: {Login}, broadcaster: {Id}).", channelLogin, broadcasterId);

                await ircSource.ConnectAsync(channelLogin!, accessToken, stoppingToken).ConfigureAwait(false);
                await eventSubSource.ConnectAsync(broadcasterId!, stoppingToken).ConfigureAwait(false);

                logger.LogInformation("Twitch live ingestion started.");
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Twitch live ingestion startup failed; retrying in {Delay}s.", RetryDelay.TotalSeconds);
                try
                {
                    await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }

        await ircSource.DisconnectAsync().ConfigureAwait(false);
        await eventSubSource.DisconnectAsync().ConfigureAwait(false);
    }
}
