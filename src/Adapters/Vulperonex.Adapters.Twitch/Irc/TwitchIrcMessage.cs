namespace Vulperonex.Adapters.Twitch.Irc;

public sealed record TwitchIrcMessage(
    IReadOnlyDictionary<string, string> Tags,
    string UserName,
    string Channel,
    string Text);
