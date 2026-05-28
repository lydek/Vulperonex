using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchUserSubscribed : ITriggerFilterMatcher
{
    public string EventTypeKey => "user.subscribed";

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

        if (filter.TryGetValue("IsGift", out var isGiftStr) && bool.TryParse(isGiftStr, out var expectedGift))
        {
            if (!triggerValues.TryGetValue("IsGift", out var actualGiftObj) || actualGiftObj == null)
            {
                return false;
            }

            if (!bool.TryParse(actualGiftObj.ToString(), out var actualGift) || actualGift != expectedGift)
            {
                return false;
            }
        }

        return true;
    }
}
