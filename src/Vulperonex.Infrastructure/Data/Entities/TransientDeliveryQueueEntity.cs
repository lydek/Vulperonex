namespace Vulperonex.Infrastructure.Data.Entities;

public sealed class TransientDeliveryQueueEntity
{
    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int ReplayCount { get; set; }
}
