using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserDonated : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "MinAmount",
    };

    public string EventTypeKey => "user.donated";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

        if (filter.TryGetValue("MinAmount", out var minAmtStr))
        {
            if (!decimal.TryParse(minAmtStr, out var minAmt))
            {
                return false;
            }

            if (!triggerValues.TryGetValue("TotalBitsGiven", out var amtObj) || amtObj == null)
            {
                return false;
            }

            if (!decimal.TryParse(amtObj.ToString(), out var actualAmt) || actualAmt < minAmt)
            {
                return false;
            }
        }

        return true;
    }
}
