using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;
using Vulperonex.Domain.Events;

namespace Vulperonex.Web.SignalR;

public sealed class OverlayEventForwarder(
    IStreamEventBus eventBus,
    IHubContext<EventsHub> eventsHub,
    IHubContext<OverlayChatHub> chatHub,
    IHubContext<OverlayAlertsHub> alertsHub,
    IOverlayHistoryService<OverlayChatPayload> chatHistory,
    IOverlayHistoryService<OverlayAlertPayload> alertsHistory,
    IClock clock,
    ILogger<OverlayEventForwarder> logger) : IHostedService
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
        var payload = new StreamEventEnvelope(
            streamEvent.EventTypeKey,
            streamEvent.EventId,
            streamEvent.Platform,
            streamEvent.OccurredAt);

        return SafeSendAsync(() => eventsHub.Clients.All.SendAsync("event", payload, cancellationToken), cancellationToken);
    }

    private async Task ForwardChatEventAsync(UserSentMessageEvent streamEvent, CancellationToken cancellationToken)
    {
        var payload = new OverlayChatPayload(
            1,
            streamEvent.EventId,
            clock.UtcNow,
            streamEvent.User.DisplayName,
            null,
            [new OverlayTextSegment("text", streamEvent.MessageText)],
            []);

        await TryPersistAsync(chatHistory, payload, cancellationToken);
        await SafeSendAsync(() => chatHub.Clients.All.SendAsync("event", payload, cancellationToken), cancellationToken);
    }

    private async Task ForwardAlertEventAsync(
        string eventId,
        string displayName,
        string eventType,
        string? tier,
        CancellationToken cancellationToken)
    {
        var payload = new OverlayAlertPayload(
            1,
            eventId,
            clock.UtcNow,
            displayName,
            eventType,
            tier);

        await TryPersistAsync(alertsHistory, payload, cancellationToken);
        await SafeSendAsync(() => alertsHub.Clients.All.SendAsync("event", payload, cancellationToken), cancellationToken);
    }

    private async Task TryPersistAsync<TPayload>(
        IOverlayHistoryService<TPayload> history,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await history.AddAsync(payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to persist overlay history payload for hub {HubName}; broadcast will continue.",
                history.HubName);
        }
    }

    private static async Task SafeSendAsync(Func<Task> send, CancellationToken cancellationToken)
    {
        try
        {
            await send();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown cancellation should not escape into the event bus subscription path.
        }
    }
}

public sealed record StreamEventEnvelope(
    string Type,
    string EventId,
    string Platform,
    DateTimeOffset OccurredAt);
