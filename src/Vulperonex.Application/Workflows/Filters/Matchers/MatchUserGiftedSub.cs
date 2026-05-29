using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserGiftedSub : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tier",
        "MinGiftCount",
    };

    public string EventTypeKey => "user.gifted_sub";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

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

        if (filter.TryGetValue("MinGiftCount", out var minCountStr))
        {
            if (!int.TryParse(minCountStr, out var minCount))
            {
                return false;
            }

            if (!triggerValues.TryGetValue("GiftCount", out var countObj) || countObj == null)
            {
                return false;
            }

            if (!int.TryParse(countObj.ToString(), out var actualCount) || actualCount < minCount)
            {
                return false;
            }
        }

        return true;
    }
}
