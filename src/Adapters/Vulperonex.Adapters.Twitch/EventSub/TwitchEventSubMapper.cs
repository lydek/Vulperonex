using System.Text;
using System.Text.Json;
using Vulperonex.Adapters.Twitch.Irc;
using Vulperonex.Domain;

namespace Vulperonex.Adapters.Twitch.EventSub;

/// <summary>
/// Translates Twitch EventSub notification <c>event</c> payloads into the
/// existing ingestion shapes (<see cref="TwitchIrcMessage"/> for chat,
/// <see cref="TwitchMockPayload"/> for everything else) so the established
/// parser / mapper / dedup / display-cache pipeline is reused verbatim.
/// </summary>
public static class TwitchEventSubMapper
{
    public const string ChatMessageType = "channel.chat.message";
    public const string FollowType = "channel.follow";
    public const string SubscribeType = "channel.subscribe";
    public const string SubscriptionGiftType = "channel.subscription.gift";
    public const string CheerType = "channel.cheer";
    public const string RaidType = "channel.raid";
    public const string RewardRedemptionAddType = "channel.channel_points_custom_reward_redemption.add";

    /// <summary>Subscription (type, version, condition keys) the host should request.</summary>
    public static IReadOnlyList<string> SupportedSubscriptionTypes { get; } =
    [
        ChatMessageType,
        FollowType,
        SubscribeType,
        SubscriptionGiftType,
        CheerType,
        RaidType,
        RewardRedemptionAddType,
    ];

    public static TwitchIrcMessage ToIrcMessage(JsonElement chatEvent, string fallbackMessageId)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["msg-id"] = GetString(chatEvent, "message_id") ?? fallbackMessageId,
            ["user-id"] = GetString(chatEvent, "chatter_user_id") ?? string.Empty,
            ["display-name"] = GetString(chatEvent, "chatter_user_name")
                ?? GetString(chatEvent, "chatter_user_login")
                ?? string.Empty,
        };

        var color = GetString(chatEvent, "color");
        if (!string.IsNullOrWhiteSpace(color))
        {
            tags["color"] = color;
        }

        var badges = BuildBadges(chatEvent);
        if (badges.Length > 0)
        {
            tags["badges"] = badges;
        }

        if (chatEvent.TryGetProperty("cheer", out var cheer)
            && cheer.ValueKind == JsonValueKind.Object
            && cheer.TryGetProperty("bits", out var bits)
            && bits.ValueKind == JsonValueKind.Number)
        {
            tags["bits"] = bits.GetInt32().ToString();
        }

        var text = chatEvent.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
                ? GetString(message, "text") ?? string.Empty
                : string.Empty;

        return new TwitchIrcMessage(
            tags,
            GetString(chatEvent, "chatter_user_login") ?? string.Empty,
            GetString(chatEvent, "broadcaster_user_login") ?? string.Empty,
            text);
    }

    public static TwitchMockPayload? ToMockPayload(string subscriptionType, JsonElement evt, string messageId)
    {
        return subscriptionType switch
        {
            FollowType => new TwitchMockPayload(
                TwitchMockPayloadKind.Followed,
                UserFrom(evt, "user_id", "user_name", "user_login"),
                SourceEventId: messageId),
            SubscribeType => new TwitchMockPayload(
                TwitchMockPayloadKind.Subscribed,
                UserFrom(evt, "user_id", "user_name", "user_login"),
                Tier: GetString(evt, "tier") ?? string.Empty,
                SourceEventId: messageId),
            SubscriptionGiftType => new TwitchMockPayload(
                TwitchMockPayloadKind.GiftedSubscription,
                UserFrom(evt, "user_id", "user_name", "user_login"),
                Tier: GetString(evt, "tier") ?? string.Empty,
                GiftCount: GetInt(evt, "total"),
                SourceEventId: messageId),
            CheerType => new TwitchMockPayload(
                TwitchMockPayloadKind.Donated,
                UserFrom(evt, "user_id", "user_name", "user_login"),
                TotalBitsGiven: GetInt(evt, "bits"),
                SourceEventId: messageId),
            RaidType => new TwitchMockPayload(
                TwitchMockPayloadKind.Raided,
                UserFrom(evt, "from_broadcaster_user_id", "from_broadcaster_user_name", "from_broadcaster_user_login"),
                ViewerCount: GetInt(evt, "viewers"),
                SourceEventId: messageId),
            RewardRedemptionAddType => new TwitchMockPayload(
                TwitchMockPayloadKind.RewardRedeemed,
                UserFrom(evt, "user_id", "user_name", "user_login"),
                RewardId: evt.TryGetProperty("reward", out var reward) ? GetString(reward, "id") : null,
                RewardTitle: evt.TryGetProperty("reward", out var rewardTitle) ? GetString(rewardTitle, "title") : null,
                RedemptionId: GetString(evt, "id") ?? messageId,
                SourceEventId: messageId),
            _ => null,
        };
    }

    private static StreamUser UserFrom(JsonElement evt, string idProp, string nameProp, string loginProp)
    {
        var userId = GetString(evt, idProp);
        var displayName = GetString(evt, nameProp) ?? GetString(evt, loginProp);
        return string.IsNullOrWhiteSpace(userId)
            ? new StreamUser("twitch", "anonymous", displayName ?? "Anonymous", StreamRole.None)
            : new StreamUser("twitch", userId, displayName ?? userId, StreamRole.None);
    }

    private static string BuildBadges(JsonElement chatEvent)
    {
        if (!chatEvent.TryGetProperty("badges", out var badges) || badges.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var badge in badges.EnumerateArray())
        {
            var setId = GetString(badge, "set_id");
            var id = GetString(badge, "id");
            if (string.IsNullOrWhiteSpace(setId) || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(setId).Append('/').Append(id);
        }

        return builder.ToString();
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed)
                ? parsed
                : 0;
    }
}
