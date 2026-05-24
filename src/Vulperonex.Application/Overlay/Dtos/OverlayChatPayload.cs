namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayChatPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string DisplayName,
    string? ColorHex,
    IReadOnlyCollection<OverlayTextSegment> Segments,
    IReadOnlyCollection<string> Badges,
    OverlayMemberSnapshot? MemberSnapshot = null);

public sealed record OverlayMemberSnapshot(
    string DisplayName,
    string? AvatarUrl,
    int CheckInCount);

public sealed record OverlayTextSegment(string Type, string Value)
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "text",
        "emote",
        "badge",
        "mention",
    };

    public string Type { get; init; } = ValidateType(Type);

    public string Value { get; init; } = Value;

    private static string ValidateType(string type)
    {
        if (!AllowedTypes.Contains(type))
        {
            throw new ArgumentException("Unsupported overlay text segment type.", nameof(type));
        }

        return type;
    }
}
