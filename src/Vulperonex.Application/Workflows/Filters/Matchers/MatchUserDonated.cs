using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserDonated : ITriggerFilterMatcher
{
    public string EventTypeKey => "user.donated";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (filter.TryGetValue("MinAmount", out var minAmtStr) && decimal.TryParse(minAmtStr, out var minAmt))
        {
            if (!triggerValues.TryGetValue("Amount", out var amtObj) || amtObj == null)
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
