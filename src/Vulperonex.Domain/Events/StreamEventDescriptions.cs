namespace Vulperonex.Domain.Events;

public static class StreamEventDescriptions
{
    private static readonly Dictionary<string, EventDescription> Descriptions = new()
    {
        [StreamEventKeys.UserSentMessage] = new("User sent a chat message", IsSystemEvent: false),
        [StreamEventKeys.UserFollowed] = new("User followed the channel", IsSystemEvent: false),
        [StreamEventKeys.UserDonated] = new("User donated", IsSystemEvent: false),
        [StreamEventKeys.UserSubscribed] = new("User subscribed to the channel", IsSystemEvent: false),
        [StreamEventKeys.UserGiftedSubscription] = new("User gifted a subscription", IsSystemEvent: false),
        [StreamEventKeys.ChannelRaided] = new("Channel was raided", IsSystemEvent: false),
        [StreamEventKeys.RewardRedeemed] = new("User redeemed a reward", IsSystemEvent: false),
        [StreamEventKeys.PlatformConnectionChanged] = new("Platform connection status changed", IsSystemEvent: true),
        [StreamEventKeys.MemberCheckedIn] = new("Member checked in", IsSystemEvent: true),
    };

    public static string? GetDescription(string eventTypeKey)
    {
        return Descriptions.TryGetValue(eventTypeKey, out var description)
            ? description.Text
            : null;
    }

    public static bool IsSystemEvent(string eventTypeKey)
    {
        return Descriptions.TryGetValue(eventTypeKey, out var description) && description.IsSystemEvent;
    }

    public static IReadOnlyCollection<string> GetWorkflowVisibleKeys()
    {
        return Descriptions
            .Where(item => !item.Value.IsSystemEvent)
            .Select(item => item.Key)
            .ToArray();
    }

    private sealed record EventDescription(string Text, bool IsSystemEvent);
}
