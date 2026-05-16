using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Domain.Events;

namespace Vulperonex.Web.SignalR;

public sealed class OverlayEventForwarder(
    IStreamEventBus eventBus,
    IHubContext<EventsHub> eventsHub,
    IHubContext<OverlayChatHub> chatHub,
    IHubContext<OverlayAlertsHub> alertsHub) : IHostedService
{
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(eventBus.Subscribe<IStreamEvent>(ForwardManagementEventAsync));
        _subscriptions.Add(eventBus.Subscribe<UserSentMessageEvent>(ForwardChatEventAsync));
        _subscriptions.Add(eventBus.Subscribe<UserFollowedEvent>((streamEvent, token) =>
            ForwardAlertEventAsync(streamEvent.EventId, streamEvent.User.DisplayName, "followed", null, token)));
        _subscriptions.Add(eventBus.Subscribe<UserSubscribedEvent>((streamEvent, token) =>
            ForwardAlertEventAsync(streamEvent.EventId, streamEvent.User.DisplayName, "subscribed", streamEvent.Tier, token)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        return Task.CompletedTask;
    }

    private Task ForwardManagementEventAsync(IStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        return eventsHub.Clients.All.SendAsync("event", streamEvent, cancellationToken);
    }

    private Task ForwardChatEventAsync(UserSentMessageEvent streamEvent, CancellationToken cancellationToken)
    {
        var payload = new OverlayChatPayload(
            1,
            streamEvent.EventId,
            DateTimeOffset.UtcNow,
            streamEvent.User.DisplayName,
            null,
            [new OverlayTextSegment("text", streamEvent.MessageText)],
            []);

        return chatHub.Clients.All.SendAsync("event", payload, cancellationToken);
    }

    private Task ForwardAlertEventAsync(
        string eventId,
        string displayName,
        string eventType,
        string? tier,
        CancellationToken cancellationToken)
    {
        var payload = new OverlayAlertPayload(
            1,
            eventId,
            DateTimeOffset.UtcNow,
            displayName,
            eventType,
            tier);

        return alertsHub.Clients.All.SendAsync("event", payload, cancellationToken);
    }
}
