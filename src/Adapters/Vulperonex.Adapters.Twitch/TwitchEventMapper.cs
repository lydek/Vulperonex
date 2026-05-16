using Vulperonex.Domain.Events;

namespace Vulperonex.Adapters.Twitch;

public static class TwitchEventMapper
{
    public static IStreamEvent Map(TwitchMockPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var eventId = string.IsNullOrWhiteSpace(payload.SourceEventId)
            ? payload.SyntheticEventId
            : payload.SourceEventId;

        return payload.Kind switch
        {
            TwitchMockPayloadKind.Message => new UserSentMessageEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                MessageText = payload.MessageText ?? string.Empty,
            },
            TwitchMockPayloadKind.Followed => new UserFollowedEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
            },
            TwitchMockPayloadKind.Donated => new UserDonatedEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                TotalBitsGiven = payload.TotalBitsGiven,
            },
            TwitchMockPayloadKind.Subscribed => new UserSubscribedEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                Tier = payload.Tier ?? string.Empty,
            },
            TwitchMockPayloadKind.GiftedSubscription => new UserGiftedSubscriptionEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                Tier = payload.Tier ?? string.Empty,
                GiftCount = payload.GiftCount,
            },
            TwitchMockPayloadKind.Raided => new ChannelRaidedEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                ViewerCount = payload.ViewerCount,
            },
            TwitchMockPayloadKind.RewardRedeemed => new RewardRedeemedEvent
            {
                EventId = eventId,
                Platform = "twitch",
                User = payload.User,
                RewardId = payload.RewardId ?? string.Empty,
                RewardTitle = payload.RewardTitle ?? string.Empty,
            },
            _ => throw new NotSupportedException($"Unsupported Twitch mock payload kind: {payload.Kind}."),
        };
    }
}
