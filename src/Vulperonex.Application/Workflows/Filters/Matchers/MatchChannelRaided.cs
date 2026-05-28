using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchChannelRaided : ITriggerFilterMatcher
{
    public string EventTypeKey => "channel.raided";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (filter.TryGetValue("MinViewers", out var minViewersStr) && int.TryParse(minViewersStr, out var minViewers))
        {
            object? viewersObj = null;
            if (!triggerValues.TryGetValue("ViewerCount", out viewersObj) &&
                !triggerValues.TryGetValue("MinViewers", out viewersObj) &&
                !triggerValues.TryGetValue("Viewers", out viewersObj))
            {
                return false;
            }

            if (viewersObj == null || !int.TryParse(viewersObj.ToString(), out var actualViewers) || actualViewers < minViewers)
            {
                return false;
            }
        }

        return true;
    }
}
