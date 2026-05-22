using Vulperonex.Domain;

namespace Vulperonex.Adapters.Twitch;

public sealed record TwitchMockPayload(
    TwitchMockPayloadKind Kind,
    StreamUser User,
    string? MessageText = null,
    int TotalBitsGiven = 0,
    string? Tier = null,
    int GiftCount = 0,
    int ViewerCount = 0,
    string? RewardId = null,
    string? RewardTitle = null,
    string? RedemptionId = null,
    string? SourceEventId = null)
{
    public string SyntheticEventId { get; init; } = TwitchSyntheticEventId.New();

    public bool UsesSyntheticEventId => string.IsNullOrWhiteSpace(SourceEventId);
}

public enum TwitchMockPayloadKind
{
    Message,
    Followed,
    Donated,
    Subscribed,
    GiftedSubscription,
    Raided,
    RewardRedeemed,
}
