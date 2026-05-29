using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserSubscribed : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tier",
    };

    public string EventTypeKey => "user.subscribed";

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

        return true;
    }
}
