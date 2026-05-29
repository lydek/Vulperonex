using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchRewardRedeemed : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "RewardName",
    };

    public string EventTypeKey => "reward.redeemed";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

        if (filter.TryGetValue("RewardName", out var expectedName))
        {
            if (!triggerValues.TryGetValue("RewardTitle", out var nameObj) || nameObj == null)
            {
                return false;
            }

            if (!string.Equals(nameObj.ToString(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
