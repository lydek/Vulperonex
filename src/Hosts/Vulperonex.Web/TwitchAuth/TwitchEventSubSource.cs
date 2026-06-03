using System.Net;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Twitch;
using Vulperonex.Domain.Events;

namespace Vulperonex.Web.TwitchAuth;

/// <summary>
/// Live Twitch alert ingestion via EventSub WebSocket (TwitchLib.EventSub.Websockets).
/// Mirrors omni-commander TwitchEventHub: typed handlers map to the adapter's
/// <see cref="TwitchMockPayload"/> and flow through <see cref="TwitchAdapter.IngestAlertAsync"/>.
/// Subscriptions are created (via the existing Helix client) once the websocket
/// session id is available.
/// </summary>
public sealed class TwitchEventSubSource(
    EventSubWebsocketClient client,
    TwitchAdapter adapter,
    IStreamEventBus eventBus,
    IServiceScopeFactory scopeFactory,
    ILogger<TwitchEventSubSource> logger)
{
    private string? _broadcasterId;
    private string? _lastSubscribedSessionId;
    private int _wired;

    public Task ConnectAsync(string broadcasterId, CancellationToken cancellationToken)
    {
        _broadcasterId = broadcasterId;

        if (Interlocked.Exchange(ref _wired, 1) == 0)
        {
            client.WebsocketConnected += OnWebsocketConnectedAsync;
            client.WebsocketDisconnected += OnWebsocketDisconnectedAsync;
            client.ChannelFollow += OnFollowAsync;
            client.ChannelSubscribe += OnSubscribeAsync;
            client.ChannelSubscriptionMessage += OnSubscriptionMessageAsync;
            client.ChannelSubscriptionGift += OnSubscriptionGiftAsync;
            client.ChannelCheer += OnCheerAsync;
            client.ChannelRaid += OnRaidAsync;
            client.ChannelPointsCustomRewardRedemptionAdd += OnRedemptionAsync;
        }

        return client.ConnectAsync();
    }

    public Task DisconnectAsync() => client.DisconnectAsync();

    private async Task OnWebsocketConnectedAsync(object? sender, WebsocketConnectedArgs e)
    {
        logger.LogInformation("Twitch EventSub WebSocket connected (Session: {Session}).", client.SessionId);
        await PublishConnectionAsync(connected: true, reason: null);

        if (e.IsRequestedReconnect)
        {
            // Subscriptions carry over a seamless reconnect.
            return;
        }

        await CreateSubscriptionsAsync();
    }

    private async Task OnWebsocketDisconnectedAsync(object? sender, WebsocketDisconnectedArgs e)
    {
        logger.LogWarning("Twitch EventSub WebSocket disconnected.");
        await PublishConnectionAsync(connected: false, reason: "eventsub_disconnected");
    }

    private async Task CreateSubscriptionsAsync()
    {
        var sessionId = client.SessionId;
        var broadcasterId = _broadcasterId;
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(broadcasterId))
        {
            return;
        }

        if (string.Equals(_lastSubscribedSessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var helix = scope.ServiceProvider.GetRequiredService<IHelixClient>();

        var authFailed = false;

        foreach (var (type, version, condition) in BuildSubscriptions(broadcasterId))
        {
            try
            {
                await helix.CreateEventSubSubscriptionAsync(type, version, condition, sessionId);
                logger.LogInformation("Twitch EventSub subscription registered: {Type}.", type);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                authFailed = true;
                logger.LogWarning(ex, "Twitch EventSub subscription {Type} rejected ({Status}); operator likely needs to re-grant scopes.", type, ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to register Twitch EventSub subscription {Type}.", type);
            }
        }

        _lastSubscribedSessionId = sessionId;

        if (authFailed)
        {
            // Surface to the UI as a distinct connection state so the operator
            // sees "authorize again" rather than a generic "reconnecting" chip.
            await PublishConnectionAsync(connected: false, reason: "auth_failed");
        }
    }

    private static IEnumerable<(string Type, string Version, IReadOnlyDictionary<string, string> Condition)> BuildSubscriptions(
        string broadcasterId)
    {
        yield return ("channel.channel_points_custom_reward_redemption.add", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId });
        yield return ("channel.raid", "1", new Dictionary<string, string> { ["to_broadcaster_user_id"] = broadcasterId });
        yield return ("channel.subscribe", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId });
        yield return ("channel.subscription.message", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId });
        yield return ("channel.subscription.gift", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId });
        yield return ("channel.follow", "2", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId, ["moderator_user_id"] = broadcasterId });
        yield return ("channel.cheer", "1", new Dictionary<string, string> { ["broadcaster_user_id"] = broadcasterId });
    }

    private Task OnFollowAsync(object? sender, ChannelFollowArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.Follow(evt.UserId, evt.UserLogin, evt.UserName));
    }

    private Task OnSubscribeAsync(object? sender, ChannelSubscribeArgs e)
    {
        var evt = e.Payload.Event;
        if (evt.IsGift)
        {
            return Task.CompletedTask; // gifts handled by the gift event
        }

        return IngestAsync(TwitchAlertPayloadFactory.Subscribe(evt.UserId, evt.UserLogin, evt.UserName, evt.Tier));
    }

    private Task OnSubscriptionMessageAsync(object? sender, ChannelSubscriptionMessageArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.Subscribe(evt.UserId, evt.UserLogin, evt.UserName, evt.Tier));
    }

    private Task OnSubscriptionGiftAsync(object? sender, ChannelSubscriptionGiftArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.GiftSub(evt.UserId, evt.UserLogin, evt.UserName, evt.Tier, evt.Total));
    }

    private Task OnCheerAsync(object? sender, ChannelCheerArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.Cheer(evt.UserId, evt.UserLogin, evt.UserName, evt.Bits));
    }

    private Task OnRaidAsync(object? sender, ChannelRaidArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.Raid(evt.FromBroadcasterUserId, evt.FromBroadcasterUserLogin, evt.FromBroadcasterUserName, evt.Viewers));
    }

    private Task OnRedemptionAsync(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        var evt = e.Payload.Event;
        return IngestAsync(TwitchAlertPayloadFactory.Redemption(evt.UserId, evt.UserLogin, evt.UserName, evt.Reward?.Id, evt.Reward?.Title, evt.Id));
    }

    private async Task IngestAsync(TwitchMockPayload payload)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cache = scope.ServiceProvider.GetRequiredService<IPlatformUserInfoCache>();
            await adapter.IngestAlertAsync(payload, new TwitchDisplayCacheUpdater(cache));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ingest Twitch EventSub alert {Kind}.", payload.Kind);
        }
    }

    private async Task PublishConnectionAsync(bool connected, string? reason)
    {
        try
        {
            await eventBus.PublishAsync(new PlatformConnectionChangedEvent
            {
                Platform = "twitch",
                IsConnected = connected,
                Reason = reason,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish Twitch EventSub connection state.");
        }
    }
}
