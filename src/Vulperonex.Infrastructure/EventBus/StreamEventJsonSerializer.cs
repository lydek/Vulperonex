using System.Text.Json;
using Vulperonex.Domain.Events;

namespace Vulperonex.Infrastructure.EventBus;

internal static class StreamEventJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IStreamEvent streamEvent)
    {
        return JsonSerializer.Serialize(streamEvent, streamEvent.GetType(), Options);
    }

    public static IStreamEvent Deserialize(string eventType, string payloadJson)
    {
        return eventType switch
        {
            StreamEventKeys.UserSentMessage => JsonSerializer.Deserialize<UserSentMessageEvent>(payloadJson, Options)!,
            StreamEventKeys.UserFollowed => JsonSerializer.Deserialize<UserFollowedEvent>(payloadJson, Options)!,
            StreamEventKeys.UserDonated => JsonSerializer.Deserialize<UserDonatedEvent>(payloadJson, Options)!,
            StreamEventKeys.UserSubscribed => JsonSerializer.Deserialize<UserSubscribedEvent>(payloadJson, Options)!,
            StreamEventKeys.UserGiftedSubscription => JsonSerializer.Deserialize<UserGiftedSubscriptionEvent>(payloadJson, Options)!,
            StreamEventKeys.ChannelRaided => JsonSerializer.Deserialize<ChannelRaidedEvent>(payloadJson, Options)!,
            StreamEventKeys.RewardRedeemed => JsonSerializer.Deserialize<RewardRedeemedEvent>(payloadJson, Options)!,
            StreamEventKeys.PlatformConnectionChanged => JsonSerializer.Deserialize<PlatformConnectionChangedEvent>(payloadJson, Options)!,
            _ => throw new InvalidOperationException($"Unknown stream event type: {eventType}"),
        };
    }
}
