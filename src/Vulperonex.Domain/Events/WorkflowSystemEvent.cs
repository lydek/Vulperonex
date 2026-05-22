namespace Vulperonex.Domain.Events;

public sealed record WorkflowSystemEvent : IStreamEvent
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();

    public required string EventTypeKey { get; init; }

    public string Platform { get; init; } = "system";

    public StreamUser? User { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public int Depth { get; init; }

    public IReadOnlyDictionary<string, string> Payload { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
