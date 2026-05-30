using Vulperonex.Adapters.Twitch;
using Vulperonex.Domain;

namespace Vulperonex.Web.TwitchAuth;

/// <summary>
/// Pure mapping from primitive EventSub fields to the adapter's
/// <see cref="TwitchMockPayload"/>. Kept socket-free so it is unit-testable
/// without TwitchLib types.
/// </summary>
public static class TwitchAlertPayloadFactory
{
    public static TwitchMockPayload Follow(string? userId, string? login, string? name, string? sourceEventId = null)
        => new(TwitchMockPayloadKind.Followed, User(userId, login, name), SourceEventId: sourceEventId);

    public static TwitchMockPayload Subscribe(string? userId, string? login, string? name, string? tier, string? sourceEventId = null)
        => new(TwitchMockPayloadKind.Subscribed, User(userId, login, name), Tier: tier ?? string.Empty, SourceEventId: sourceEventId);

    public static TwitchMockPayload GiftSub(string? userId, string? login, string? name, string? tier, int total, string? sourceEventId = null)
        => new(TwitchMockPayloadKind.GiftedSubscription, User(userId, login, name), Tier: tier ?? string.Empty, GiftCount: total, SourceEventId: sourceEventId);

    public static TwitchMockPayload Cheer(string? userId, string? login, string? name, int bits, string? sourceEventId = null)
        => new(TwitchMockPayloadKind.Donated, User(userId, login, name), TotalBitsGiven: bits, SourceEventId: sourceEventId);

    public static TwitchMockPayload Raid(string? raiderId, string? raiderLogin, string? raiderName, int viewers, string? sourceEventId = null)
        => new(TwitchMockPayloadKind.Raided, User(raiderId, raiderLogin, raiderName), ViewerCount: viewers, SourceEventId: sourceEventId);

    public static TwitchMockPayload Redemption(
        string? userId,
        string? login,
        string? name,
        string? rewardId,
        string? rewardTitle,
        string? redemptionId)
        => new(
            TwitchMockPayloadKind.RewardRedeemed,
            User(userId, login, name),
            RewardId: rewardId,
            RewardTitle: rewardTitle,
            RedemptionId: redemptionId,
            SourceEventId: redemptionId);

    private static StreamUser User(string? userId, string? login, string? name)
    {
        var displayName = !string.IsNullOrWhiteSpace(name)
            ? name
            : (!string.IsNullOrWhiteSpace(login) ? login : userId);
        return string.IsNullOrWhiteSpace(userId)
            ? new StreamUser("twitch", "anonymous", displayName ?? "Anonymous", StreamRole.None)
            : new StreamUser("twitch", userId, displayName ?? userId, StreamRole.None);
    }
}
