namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayAlertPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string DisplayName,
    string EventType,
    string? Tier,
    bool Replayed = false);
