using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.Time;

namespace Vulperonex.Web.SignalR;

public sealed class PlatformConnectionNotifier(
    IHubContext<EventsHub> eventsHub,
    IClock clock)
{
    public const string EventTypeKey = "platform.connection_changed";

    public Task NotifyAsync(string platform, bool connected, CancellationToken cancellationToken = default)
    {
        var envelope = new PlatformConnectionEnvelope(
            EventTypeKey,
            Guid.NewGuid().ToString("N"),
            platform,
            clock.UtcNow,
            connected);

        return eventsHub.Clients.All.SendAsync("event", envelope, cancellationToken);
    }
}

public sealed record PlatformConnectionEnvelope(
    string Type,
    string EventId,
    string Platform,
    DateTimeOffset OccurredAt,
    bool Connected);
