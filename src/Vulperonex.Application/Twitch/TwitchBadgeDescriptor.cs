namespace Vulperonex.Application.Twitch;

public sealed record TwitchBadgeDescriptor(
    string Key,
    string SetId,
    string Version,
    string ImageUrl1x,
    string? Title,
    string? Description,
    bool IsChannel);
