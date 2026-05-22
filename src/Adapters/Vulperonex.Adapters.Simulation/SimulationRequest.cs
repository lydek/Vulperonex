using Vulperonex.Domain;

namespace Vulperonex.Adapters.Simulation;

public sealed record SimulationRequest
{
    private SimulationRequest(SimulationKind kind, string platform, StreamUser user)
    {
        Kind = kind;
        Platform = platform;
        User = user;
    }

    public SimulationKind Kind { get; }
    public string Platform { get; }
    public StreamUser User { get; }
    public string? MessageText { get; private init; }
    public string? Tier { get; private init; }
    public int TotalBitsGiven { get; private init; }
    public int GiftCount { get; private init; }
    public int ViewerCount { get; private init; }
    public string? RewardId { get; private init; }
    public string? RewardTitle { get; private init; }
    public string? RedemptionId { get; private init; }

    public static SimulationRequest Message(string platform, StreamUser user, string messageText)
    {
        return new SimulationRequest(SimulationKind.Message, platform, user)
        {
            MessageText = messageText,
        };
    }

    public static SimulationRequest Followed(string platform, StreamUser user)
    {
        return new SimulationRequest(SimulationKind.Followed, platform, user);
    }

    public static SimulationRequest Donated(string platform, StreamUser user, int totalBitsGiven)
    {
        return new SimulationRequest(SimulationKind.Donated, platform, user)
        {
            TotalBitsGiven = totalBitsGiven,
        };
    }

    public static SimulationRequest Subscribed(string platform, StreamUser user, string tier)
    {
        return new SimulationRequest(SimulationKind.Subscribed, platform, user)
        {
            Tier = tier,
        };
    }

    public static SimulationRequest GiftedSubscription(string platform, StreamUser user, string tier, int giftCount)
    {
        return new SimulationRequest(SimulationKind.GiftedSubscription, platform, user)
        {
            Tier = tier,
            GiftCount = giftCount,
        };
    }

    public static SimulationRequest Raided(string platform, StreamUser user, int viewerCount)
    {
        return new SimulationRequest(SimulationKind.Raided, platform, user)
        {
            ViewerCount = viewerCount,
        };
    }

    public static SimulationRequest RewardRedeemed(
        string platform,
        StreamUser user,
        string rewardId,
        string rewardTitle,
        string redemptionId = "redemption-1")
    {
        return new SimulationRequest(SimulationKind.RewardRedeemed, platform, user)
        {
            RewardId = rewardId,
            RewardTitle = rewardTitle,
            RedemptionId = redemptionId,
        };
    }
}
