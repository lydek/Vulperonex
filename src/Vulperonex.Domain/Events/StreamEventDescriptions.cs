namespace Vulperonex.Domain.Events;

public static class StreamEventDescriptions
{
    private static readonly Dictionary<string, EventDescription> Descriptions = new()
    {
        [StreamEventKeys.UserSentMessage] = new("使用者發送了聊天訊息", IsSystemEvent: false),
        [StreamEventKeys.UserFollowed] = new("使用者追隨了頻道", IsSystemEvent: false),
        [StreamEventKeys.UserDonated] = new("使用者進行了斗內", IsSystemEvent: false),
        [StreamEventKeys.UserSubscribed] = new("使用者訂閱了頻道", IsSystemEvent: false),
        [StreamEventKeys.UserGiftedSubscription] = new("使用者贈送了訂閱", IsSystemEvent: false),
        [StreamEventKeys.ChannelRaided] = new("頻道被突襲", IsSystemEvent: false),
        [StreamEventKeys.RewardRedeemed] = new("使用者兌換了獎勵", IsSystemEvent: false),
        [StreamEventKeys.PlatformConnectionChanged] = new("平台連線狀態變更", IsSystemEvent: true),
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
