namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayWidgetPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string WidgetType,
    string OverlayTarget,
    string DisplayText,
    string Severity,
    int? DurationMs);
