using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchRewardRedeemed : ITriggerFilterMatcher
{
    public string EventTypeKey => "reward.redeemed";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (filter.TryGetValue("RewardName", out var expectedName))
        {
            object? nameObj = null;
            if (!triggerValues.TryGetValue("RewardTitle", out nameObj) &&
                !triggerValues.TryGetValue("RewardName", out nameObj) &&
                !triggerValues.TryGetValue("Title", out nameObj) &&
                !triggerValues.TryGetValue("Name", out nameObj))
            {
                return false;
            }

            if (nameObj == null || !string.Equals(nameObj.ToString(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
