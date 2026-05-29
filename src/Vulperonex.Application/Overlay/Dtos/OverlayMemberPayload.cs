using System;

namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayMemberPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string DisplayName,
    string? AvatarUrl,
    int CheckInCount,
    int RoundIndex,
    int StampSlotInRound);
