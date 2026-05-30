using System.Globalization;
using System.Text.RegularExpressions;
using Vulperonex.Adapters.Twitch.Display;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch.Irc;

public static partial class TwitchIrcMessageParser
{
    private static readonly HashSet<string> AllowedSegmentTypes = new(StringComparer.Ordinal)
    {
        "text",
        "emote",
        "badge",
        "mention",
    };

    public static TwitchChatParseResult Parse(TwitchIrcMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var platformUserId = GetTag(message, "user-id", message.UserName);
        var displayName = GetTag(message, "display-name", message.UserName);
        var badges = NormalizeBadges(GetTag(message, "badges", string.Empty));
        var color = NormalizeColor(GetTag(message, "color", string.Empty));
        var bitsTotal = NormalizeLong(GetTag(message, "user.bits_total", GetTag(message, "bits", "0")));
        var isSubscriber = string.Equals(GetTag(message, "user.is_subscriber", string.Empty), "true", StringComparison.OrdinalIgnoreCase)
            || badges.Any(badge => badge.StartsWith("subscriber/", StringComparison.Ordinal));

        var parsedSegments = ParseSegments(message.Text, GetTag(message, "emotes", string.Empty));
        var segments = parsedSegments.Select(s => new MessageSegment(s.Type, s.Value)).ToList();

        var streamEvent = new UserSentMessageEvent
        {
            EventId = GetTag(message, "msg-id", TwitchSyntheticEventId.New()),
            Platform = "twitch",
            User = new StreamUser("twitch", platformUserId, displayName, RolesFromBadges(badges)),
            MessageText = message.Text,
            Segments = segments
        };

        var hints = new TwitchDisplayHints(
            color,
            badges,
            parsedSegments,
            GetTag(message, "user.avatar", string.Empty) is { Length: > 0 } avatar ? avatar : null,
            isSubscriber,
            bitsTotal);

        return new TwitchChatParseResult(streamEvent, hints);
    }

    public static bool IsAllowedSegmentType(string type)
    {
        return AllowedSegmentTypes.Contains(type);
    }

    private static string GetTag(TwitchIrcMessage message, string key, string fallback)
    {
        return message.Tags.TryGetValue(key, out var value) ? value : fallback;
    }

    private static string? NormalizeColor(string value)
    {
        return HexColorRegex().IsMatch(value) ? value : null;
    }

    private static long NormalizeLong(string value)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? parsed
            : 0;
    }

    private static string[] NormalizeBadges(string badges)
    {
        if (string.IsNullOrWhiteSpace(badges))
        {
            return [];
        }

        return badges
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeBadge)
            .Where(static badge => badge is not null)
            .Select(static badge => badge!)
            .Take(20)
            .ToArray();
    }

    private static string? NormalizeBadge(string badge)
    {
        var slashIndex = badge.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex <= 0 || slashIndex == badge.Length - 1)
        {
            return null;
        }

        var id = badge[..slashIndex];
        var value = badge[(slashIndex + 1)..];
        return BadgePartRegex().IsMatch(id) && BadgePartRegex().IsMatch(value) && value.Length <= 64
            ? $"{id}/{value}"
            : null;
    }

    private static StreamRole RolesFromBadges(IEnumerable<string> badges)
    {
        var roles = StreamRole.None;
        foreach (var badge in badges)
        {
            if (badge.StartsWith("moderator/", StringComparison.Ordinal))
            {
                roles |= StreamRole.Moderator;
            }
            else if (badge.StartsWith("broadcaster/", StringComparison.Ordinal))
            {
                roles |= StreamRole.Broadcaster;
            }
            else if (badge.StartsWith("vip/", StringComparison.Ordinal))
            {
                roles |= StreamRole.Vip;
            }
            else if (badge.StartsWith("subscriber/", StringComparison.Ordinal))
            {
                roles |= StreamRole.Subscriber;
            }
        }

        return roles;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[A-Za-z0-9_/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BadgePartRegex();

    private static IReadOnlyList<DisplayHintSegment> ParseSegments(string text, string emotesTag)
    {
        if (string.IsNullOrWhiteSpace(emotesTag) || string.IsNullOrWhiteSpace(text))
        {
            return [new DisplayHintSegment("text", text)];
        }

        try
        {
            var emoteOccurrences = new List<(int Start, int End, string EmoteId)>();
            var parts = emotesTag.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var colonIndex = part.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex <= 0) continue;

                var id = part[..colonIndex];
                var positionsStr = part[(colonIndex + 1)..];
                var positions = positionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pos in positions)
                {
                    var dashIndex = pos.IndexOf('-', StringComparison.Ordinal);
                    if (dashIndex <= 0) continue;

                    if (int.TryParse(pos[..dashIndex], out var start) &&
                        int.TryParse(pos[(dashIndex + 1)..], out var end))
                    {
                        if (start >= 0 && end < text.Length && start <= end)
                        {
                            emoteOccurrences.Add((start, end, id));
                        }
                    }
                }
            }

            if (emoteOccurrences.Count == 0)
            {
                return [new DisplayHintSegment("text", text)];
            }

            var sorted = emoteOccurrences.OrderBy(o => o.Start).ToList();
            var segments = new List<DisplayHintSegment>();
            var lastIdx = 0;

            foreach (var occ in sorted)
            {
                if (occ.Start < lastIdx)
                {
                    continue;
                }

                if (occ.Start > lastIdx)
                {
                    segments.Add(new DisplayHintSegment("text", text[lastIdx..occ.Start]));
                }

                var url = $"https://static-cdn.jtvnw.net/emoticons/v2/{occ.EmoteId}/default/dark/1.0";
                segments.Add(new DisplayHintSegment("emote", url));
                lastIdx = occ.End + 1;
            }

            if (lastIdx < text.Length)
            {
                segments.Add(new DisplayHintSegment("text", text[lastIdx..]));
            }

            return segments;
        }
        catch
        {
            return [new DisplayHintSegment("text", text)];
        }
    }
}

public sealed record TwitchChatParseResult(UserSentMessageEvent Event, TwitchDisplayHints DisplayHints);
