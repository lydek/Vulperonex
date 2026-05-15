namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayChatPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string DisplayName,
    string? ColorHex,
    IReadOnlyCollection<OverlayTextSegment> Segments,
    IReadOnlyCollection<string> Badges);

public sealed record OverlayTextSegment(string Type, string Value);
