namespace Vulperonex.Domain.Events;

public sealed record RewardRedeemedEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.RewardRedeemed;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
