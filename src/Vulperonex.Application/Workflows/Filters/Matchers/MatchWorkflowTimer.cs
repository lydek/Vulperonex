using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchWorkflowTimer : ITriggerFilterMatcher
{
    public string EventTypeKey => "workflow.timer";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (filter.TryGetValue("TimerName", out var expectedName))
        {
            object? nameObj = null;
            if (!triggerValues.TryGetValue("TimerName", out nameObj) &&
                !triggerValues.TryGetValue("Timer", out nameObj) &&
                !triggerValues.TryGetValue("Payload.timerName", out nameObj))
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
