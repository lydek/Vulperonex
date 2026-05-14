namespace Vulperonex.Domain.Events;

public sealed record UserGiftedSubscriptionEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.UserGiftedSubscription;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public string Tier { get; init; } = string.Empty;
    public int GiftCount { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
