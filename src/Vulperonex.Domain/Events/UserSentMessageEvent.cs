namespace Vulperonex.Domain.Events;

public sealed record UserSentMessageEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.UserSentMessage;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
