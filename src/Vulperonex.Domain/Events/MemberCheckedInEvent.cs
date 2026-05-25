using System;

namespace Vulperonex.Domain.Events;

public sealed record MemberCheckedInEvent : IStreamEvent, ICooldownSkippable
{
    public string EventId { get; init; } = StreamEventId.NewUlidString();
    public string EventTypeKey => StreamEventKeys.MemberCheckedIn;
    public required string Platform { get; init; }
    public required StreamUser User { get; init; }
    public string? AvatarUrl { get; init; }
    public int CheckInCount { get; init; }
    public int TotalLoyalty { get; init; }
    public int RoundIndex { get; init; }
    public int StampSlotInRound { get; init; }
    public bool SkipCooldown { get; init; } = false;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
