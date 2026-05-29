using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchWorkflowTimer : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimerId",
    };

    public string EventTypeKey => "workflow.timer";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

        if (filter.TryGetValue("TimerId", out var expectedId))
        {
            if (!triggerValues.TryGetValue("Payload.TimerId", out var idObj) || idObj == null)
            {
                return false;
            }

            if (!string.Equals(idObj.ToString(), expectedId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
