namespace Vulperonex.Domain.Events;

public sealed record UserDonatedEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.UserDonated;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public int TotalBitsGiven { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
