using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchChannelRaided : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "MinViewers",
    };

    public string EventTypeKey => "channel.raided";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

        if (filter.TryGetValue("MinViewers", out var minViewersStr))
        {
            if (!int.TryParse(minViewersStr, out var minViewers))
            {
                return false;
            }

            if (!triggerValues.TryGetValue("ViewerCount", out var viewersObj) || viewersObj == null)
            {
                return false;
            }

            if (!int.TryParse(viewersObj.ToString(), out var actualViewers) || actualViewers < minViewers)
            {
                return false;
            }
        }

        return true;
    }
}
