namespace Vulperonex.Domain.Events;

public static class StreamEventKeys
{
    public const string UserSentMessage = "user.message";
    public const string UserFollowed = "user.followed";
    public const string UserDonated = "user.donated";
    public const string UserSubscribed = "user.subscribed";
    public const string UserGiftedSubscription = "user.gifted_sub";
    public const string ChannelRaided = "channel.raided";
    public const string RewardRedeemed = "reward.redeemed";
    public const string PlatformConnectionChanged = "platform.connection_changed";
}
