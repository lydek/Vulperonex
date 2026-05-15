namespace Vulperonex.Adapters.Twitch.Display;

public sealed record TwitchDisplayHints(
    string? ColorHex,
    IReadOnlyCollection<string> Badges,
    IReadOnlyCollection<DisplayHintSegment> Segments,
    string? AvatarUrl,
    bool IsSubscriber,
    long TotalBitsGiven);
