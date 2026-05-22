namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayEffectPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string EffectId,
    int? DurationMs);
