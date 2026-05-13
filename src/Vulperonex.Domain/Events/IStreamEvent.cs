namespace Vulperonex.Domain.Events;

public interface IStreamEvent
{
    string EventId { get; }

    string EventTypeKey { get; }

    string Platform { get; }

    StreamUser? User { get; }

    DateTimeOffset OccurredAt { get; }
}
