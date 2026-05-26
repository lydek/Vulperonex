using System.Reactive.Linq;
using Vulperonex.Adapters.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;
using Vulperonex.Application.Twitch;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;

namespace Vulperonex.Web.SignalR;

public sealed class OverlayEventForwarder(
    IStreamEventBus eventBus,
    IHubContext<EventsHub> eventsHub,
    IHubContext<OverlayChatHub> chatHub,
    IHubContext<OverlayAlertsHub> alertsHub,
    IHubContext<OverlayMemberHub> memberHub,
    IOverlayHistoryService<OverlayChatPayload> chatHistory,
    IOverlayHistoryService<OverlayAlertPayload> alertsHistory,
    IOverlayHistoryService<OverlayMemberPayload> memberHistory,
    IServiceScopeFactory scopeFactory,
    IPlatformBadgeCache badgeCache,
    IClock clock,
    ILogger<OverlayEventForwarder> logger) : IHostedService
{
    private readonly List<IDisposable> _subscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var stream = eventBus.Events;

        // Each Subscribe lambda is synchronous push from the bus dispatch
        // loop. Forwarding to SignalR is fire-and-forget on purpose — the
        // hub has its own buffering, and SafeSendAsync swallows shutdown
        // cancellation so it cannot escape back into Subject.OnNext.
        _subscriptions.Add(stream.Subscribe(streamEvent =>
            _ = ForwardManagementEventAsync(streamEvent, cancellationToken)));

        _subscriptions.Add(stream.OfType<UserSentMessageEvent>().Subscribe(streamEvent =>
            _ = ForwardChatEventAsync(streamEvent, cancellationToken)));

        _subscriptions.Add(stream.OfType<UserFollowedEvent>().Subscribe(streamEvent =>
            _ = ForwardAlertEventAsync(streamEvent.EventId, streamEvent.User.DisplayName, "followed", null, cancellationToken)));

        _subscriptions.Add(stream.OfType<UserSubscribedEvent>().Subscribe(streamEvent =>
            _ = ForwardAlertEventAsync(streamEvent.EventId, streamEvent.User.DisplayName, "subscribed", streamEvent.Tier, cancellationToken)));

        _subscriptions.Add(stream.OfType<MemberCheckedInEvent>().Subscribe(streamEvent =>
            _ = ForwardMemberCheckInEventAsync(streamEvent, cancellationToken)));

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
        var memberSnapshot = await TryResolveMemberSnapshotAsync(streamEvent, cancellationToken);
        var badgeUrls = ResolveBadgeUrls(memberSnapshot?.Badges);
        var payload = new OverlayChatPayload(
            1,
            streamEvent.EventId,
            clock.UtcNow,
            streamEvent.User.DisplayName,
            memberSnapshot?.ColorHex,
            [new OverlayTextSegment("text", streamEvent.MessageText)],
            badgeUrls,
            ExtractRoles(streamEvent.User.Roles),
            memberSnapshot?.Snapshot);

        await TryPersistAsync(chatHistory, payload, cancellationToken);
        await SafeSendAsync(() => chatHub.Clients.All.SendAsync("event", payload, cancellationToken), cancellationToken);
    }

    private async Task<ResolvedMemberSnapshot?> TryResolveMemberSnapshotAsync(
        UserSentMessageEvent streamEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var members = scope.ServiceProvider.GetRequiredService<IMemberQueryService>();
            var displayCache = scope.ServiceProvider.GetRequiredService<IPlatformUserInfoCache>();
            var identity = PlatformIdentity.Create(streamEvent.Platform, streamEvent.User.UserId);
            var member = await members.FindByIdentityAsync(identity, cancellationToken);
            var display = await displayCache.GetAsync(identity.Platform, identity.PlatformUserId, cancellationToken);

            return new ResolvedMemberSnapshot(
                display?.ColorHex,
                display?.Badges ?? [],
                member is null
                    ? null
                    : new OverlayMemberSnapshot(
                        streamEvent.User.DisplayName,
                        display?.AvatarUrl,
                        member.Loyalty.CheckInCount));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve chat member snapshot for {Platform}/{UserId}.",
                streamEvent.Platform,
                streamEvent.User.UserId);
            return null;
        }
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

    private async Task ForwardMemberCheckInEventAsync(
        MemberCheckedInEvent streamEvent,
        CancellationToken cancellationToken)
    {
        var payload = new OverlayMemberPayload(
            SchemaVersion: 1,
            DisplayName: streamEvent.User.DisplayName,
            AvatarUrl: streamEvent.AvatarUrl,
            CheckInCount: streamEvent.CheckInCount,
            RoundIndex: streamEvent.RoundIndex,
            StampSlotInRound: streamEvent.StampSlotInRound);

        await TryPersistAsync(memberHistory, payload, cancellationToken);
        await SafeSendAsync(() => memberHub.Clients.All.SendAsync("event", payload, cancellationToken), cancellationToken);
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

    private IReadOnlyCollection<string> ResolveBadgeUrls(IReadOnlyCollection<string>? badgeKeys)
    {
        if (badgeKeys is null || badgeKeys.Count == 0)
        {
            return [];
        }

        var urls = new List<string>(badgeKeys.Count);
        foreach (var key in badgeKeys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            // Accept pre-resolved URLs (legacy callers may already pass them).
            if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(key);
                continue;
            }

            var url = badgeCache.GetUrl(key);
            if (!string.IsNullOrWhiteSpace(url))
            {
                urls.Add(url);
            }
            else
            {
                // Cache miss (e.g. Twitch OAuth not yet completed or badge sync pending) —
                // emit the raw badge key (set/version) so overlay can still tag the row.
                urls.Add(key);
            }
        }

        return urls;
    }

    private static IReadOnlyCollection<string> ExtractRoles(StreamRole roles)
    {
        var resolved = new List<string>(4);

        if (roles.HasFlag(StreamRole.Subscriber)) resolved.Add("Subscriber");
        if (roles.HasFlag(StreamRole.Moderator)) resolved.Add("Moderator");
        if (roles.HasFlag(StreamRole.Vip)) resolved.Add("Vip");
        if (roles.HasFlag(StreamRole.Follower)) resolved.Add("Follower");

        return resolved;
    }
}

public sealed record StreamEventEnvelope(
    string Type,
    string EventId,
    string Platform,
    DateTimeOffset OccurredAt,
    string? Key = null,
    string? Value = null);

internal sealed record ResolvedMemberSnapshot(
    string? ColorHex,
    IReadOnlyCollection<string> Badges,
    OverlayMemberSnapshot? Snapshot);
