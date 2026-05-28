using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserGiftedSub : ITriggerFilterMatcher
{
    public string EventTypeKey => "user.gifted_sub";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (filter.TryGetValue("Tier", out var expectedTier))
        {
            if (!triggerValues.TryGetValue("Tier", out var actualTierObj) || actualTierObj == null)
            {
                return false;
            }

            if (!string.Equals(actualTierObj.ToString(), expectedTier, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filter.TryGetValue("MinGiftCount", out var minCountStr) && int.TryParse(minCountStr, out var minCount))
        {
            object? countObj = null;
            if (!triggerValues.TryGetValue("GiftCount", out countObj) &&
                !triggerValues.TryGetValue("MinGiftCount", out countObj) &&
                !triggerValues.TryGetValue("Count", out countObj))
            {
                return false;
            }

            if (countObj == null || !int.TryParse(countObj.ToString(), out var actualCount) || actualCount < minCount)
            {
                return false;
            }
        }

        return true;
    }
}
