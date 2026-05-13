namespace Vulperonex.Domain.Events;

public sealed record UserGiftedSubscriptionEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.UserGiftedSubscription;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
