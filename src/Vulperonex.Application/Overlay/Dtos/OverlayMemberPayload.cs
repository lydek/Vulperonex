namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayMemberPayload(
    int SchemaVersion,
    string DisplayName,
    string? AvatarUrl,
    int CheckInCount);
