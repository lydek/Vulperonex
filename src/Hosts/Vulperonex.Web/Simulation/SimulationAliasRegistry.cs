using Vulperonex.Adapters.Simulation;
using Vulperonex.Domain.Events;

namespace Vulperonex.Web.Simulation;

public sealed class SimulationAliasRegistry
{
    private static readonly IReadOnlyDictionary<string, SimulationAlias> Aliases =
        new Dictionary<string, SimulationAlias>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat"] = new("chat", StreamEventKeys.UserSentMessage, SimulationKind.Message),
            ["follow"] = new("follow", StreamEventKeys.UserFollowed, SimulationKind.Followed),
            ["sub"] = new("sub", StreamEventKeys.UserSubscribed, SimulationKind.Subscribed),
            ["giftsub"] = new("giftsub", StreamEventKeys.UserGiftedSubscription, SimulationKind.GiftedSubscription),
            ["raid"] = new("raid", StreamEventKeys.ChannelRaided, SimulationKind.Raided),
            ["bits"] = new("bits", StreamEventKeys.UserDonated, SimulationKind.Donated),
            ["redeem"] = new("redeem", StreamEventKeys.RewardRedeemed, SimulationKind.RewardRedeemed),
        };

    public IReadOnlyCollection<SimulationAlias> GetAll() => Aliases.Values.OrderBy(alias => alias.Alias).ToArray();

    public SimulationAlias? Find(string alias)
    {
        return Aliases.TryGetValue(alias, out var value) ? value : null;
    }

    public bool IsSimulatable(string eventTypeKey)
    {
        return Aliases.Values.Any(alias => alias.EventTypeKey == eventTypeKey);
    }
}

public sealed record SimulationAlias(string Alias, string EventTypeKey, SimulationKind Kind);
