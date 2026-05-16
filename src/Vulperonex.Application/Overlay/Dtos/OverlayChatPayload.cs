namespace Vulperonex.Application.Overlay.Dtos;

public sealed record OverlayChatPayload(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string DisplayName,
    string? ColorHex,
    IReadOnlyCollection<OverlayTextSegment> Segments,
    IReadOnlyCollection<string> Badges);

public sealed record OverlayTextSegment
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "text",
        "emote",
        "badge",
        "mention",
    };

    public OverlayTextSegment(string type, string value)
    {
        if (!AllowedTypes.Contains(type))
        {
            throw new ArgumentException("Unsupported overlay text segment type.", nameof(type));
        }

        Type = type;
        Value = value;
    }

    public string Type { get; }

    public string Value { get; }
}
