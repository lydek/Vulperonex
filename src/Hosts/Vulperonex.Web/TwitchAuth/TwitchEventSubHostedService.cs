using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Adapters.Twitch.EventSub;
using Vulperonex.Adapters.Twitch.Reconnect;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Web.TwitchAuth;

/// <summary>
/// Maintains the live Twitch EventSub WebSocket connection, the missing
/// ingestion source the docs require. Reads the EventSub welcome/notification
/// protocol, registers the supported subscriptions via Helix, and routes each
/// notification into <see cref="TwitchAdapter.IngestEventSubNotificationAsync"/>.
/// </summary>
public sealed class TwitchEventSubHostedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<TwitchEventSubHostedService> logger) : BackgroundService
{
    private const string DefaultWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register Twitch event types regardless of connection so the system
        // event (platform.connection.changed) and metadata exist on every boot.
        await using (var bootScope = serviceProvider.CreateAsyncScope())
        {
            var adapter = bootScope.ServiceProvider.GetRequiredService<TwitchAdapter>();
            await adapter.StartAsync(stoppingToken).ConfigureAwait(false);
        }

        if (!configuration.GetValue("Twitch:EventSub:Enabled", true))
        {
            logger.LogInformation("Twitch EventSub ingestion disabled via Twitch:EventSub:Enabled.");
            return;
        }

        var broadcasterId = configuration["Twitch:BroadcasterId"];
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            logger.LogInformation("Skipping Twitch EventSub ingestion: Twitch:BroadcasterId not configured.");
            return;
        }

        await using (var authScope = serviceProvider.CreateAsyncScope())
        {
            var tokenProvider = authScope.ServiceProvider.GetRequiredService<TwitchAccessTokenProvider>();
            await tokenProvider.RefreshOnStartupAsync(stoppingToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tokenProvider.AccessToken))
            {
                logger.LogInformation("Skipping Twitch EventSub ingestion: OAuth authorization required.");
                return;
            }
        }

        var backoff = new ReconnectBackoffPolicy();
        var url = DefaultWebSocketUrl;
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var reconnectUrl = await RunConnectionAsync(
                    url,
                    broadcasterId,
                    isReconnect: !string.Equals(url, DefaultWebSocketUrl, StringComparison.Ordinal),
                    stoppingToken).ConfigureAwait(false);

                attempt = 0;

                if (!string.IsNullOrWhiteSpace(reconnectUrl))
                {
                    // Seamless reconnect requested by Twitch: connect to the new
                    // URL without re-subscribing (subscriptions carry over).
                    url = reconnectUrl;
                    continue;
                }

                url = DefaultWebSocketUrl;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = backoff.GetDelay(attempt);
                logger.LogWarning(
                    ex,
                    "Twitch EventSub connection dropped; reconnecting in {Delay:n1}s (attempt {Attempt}).",
                    delay.TotalSeconds,
                    attempt);
                url = DefaultWebSocketUrl;
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <returns>A reconnect URL when Twitch requests a seamless reconnect; otherwise <c>null</c>.</returns>
    private async Task<string?> RunConnectionAsync(
        string url,
        string broadcasterId,
        bool isReconnect,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Twitch EventSub WebSocket connected ({Url}).", url);

        var buffer = new byte[16 * 1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var json = await ReceiveTextMessageAsync(socket, buffer, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("metadata", out var metadata))
            {
                continue;
            }

            var messageType = metadata.TryGetProperty("message_type", out var typeElement)
                ? typeElement.GetString()
                : null;

            switch (messageType)
            {
                case "session_welcome":
                    if (!isReconnect
                        && root.TryGetProperty("payload", out var welcomePayload)
                        && welcomePayload.TryGetProperty("session", out var session)
                        && session.TryGetProperty("id", out var sessionId)
                        && sessionId.GetString() is { Length: > 0 } id)
                    {
                        await CreateSubscriptionsAsync(id, broadcasterId, cancellationToken).ConfigureAwait(false);
                    }

                    break;

                case "session_keepalive":
                    break;

                case "session_reconnect":
                    if (root.TryGetProperty("payload", out var reconnectPayload)
                        && reconnectPayload.TryGetProperty("session", out var reconnectSession)
                        && reconnectSession.TryGetProperty("reconnect_url", out var reconnectUrl))
                    {
                        logger.LogInformation("Twitch EventSub requested seamless reconnect.");
                        return reconnectUrl.GetString();
                    }

                    break;

                case "notification":
                    await HandleNotificationAsync(metadata, root, cancellationToken).ConfigureAwait(false);
                    break;

                case "revocation":
                    logger.LogWarning(
                        "Twitch EventSub subscription revoked: {Payload}",
                        root.TryGetProperty("payload", out var revocation) ? revocation.ToString() : "(none)");
                    break;
            }
        }

        return null;
    }

    private async Task HandleNotificationAsync(
        JsonElement metadata,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var subscriptionType = metadata.TryGetProperty("subscription_type", out var typeElement)
            ? typeElement.GetString()
            : null;
        var messageId = metadata.TryGetProperty("message_id", out var idElement)
            ? idElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(subscriptionType)
            || !root.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty("event", out var streamEvent))
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var adapter = scope.ServiceProvider.GetRequiredService<TwitchAdapter>();
        var cache = scope.ServiceProvider.GetRequiredService<IPlatformUserInfoCache>();
        var updater = new TwitchDisplayCacheUpdater(cache);

        try
        {
            await adapter.IngestEventSubNotificationAsync(
                subscriptionType,
                messageId ?? string.Empty,
                streamEvent,
                updater,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ingest Twitch EventSub notification {Type}.", subscriptionType);
        }
    }

    private async Task CreateSubscriptionsAsync(
        string sessionId,
        string broadcasterId,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var helix = scope.ServiceProvider.GetRequiredService<IHelixClient>();

        foreach (var (type, version, condition) in BuildSubscriptions(broadcasterId))
        {
            try
            {
                await helix.CreateEventSubSubscriptionAsync(type, version, condition, sessionId, cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation("Twitch EventSub subscription registered: {Type}.", type);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to register Twitch EventSub subscription {Type}.", type);
            }
        }
    }

    private static IEnumerable<(string Type, string Version, IReadOnlyDictionary<string, string> Condition)> BuildSubscriptions(
        string broadcasterId)
    {
        yield return (TwitchEventSubMapper.ChatMessageType, "1", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
            ["user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.FollowType, "2", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
            ["moderator_user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.SubscribeType, "1", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.SubscriptionGiftType, "1", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.CheerType, "1", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.RaidType, "1", new Dictionary<string, string>
        {
            ["to_broadcaster_user_id"] = broadcasterId,
        });
        yield return (TwitchEventSubMapper.RewardRedemptionAddType, "1", new Dictionary<string, string>
        {
            ["broadcaster_user_id"] = broadcasterId,
        });
    }

    private static async Task<string?> ReceiveTextMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
