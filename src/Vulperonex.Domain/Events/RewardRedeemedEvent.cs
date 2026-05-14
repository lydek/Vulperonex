namespace Vulperonex.Domain.Events;

public sealed record RewardRedeemedEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.RewardRedeemed;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public string RewardId { get; init; } = string.Empty;
    public string RewardTitle { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
