namespace Vulperonex.Domain.Events;

public sealed record PlatformConnectionChangedEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.PlatformConnectionChanged;
    public required string Platform { get; init; }
    public required bool IsConnected { get; init; }
    public string? Reason { get; init; }
    public StreamUser? User => null;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
