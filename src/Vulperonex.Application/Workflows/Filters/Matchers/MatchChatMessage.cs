using System;
using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

public sealed class MatchChatMessage : ITriggerFilterMatcher
{
    private static readonly HashSet<string> KnownFilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "CommandName",
        "MessageText",
        "Prefix",
    };

    public string EventTypeKey => "user.message";

    public bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues)
    {
        if (!TriggerFilterMatcherGuards.ContainsOnlyKnownKeys(filter, KnownFilterKeys))
        {
            return false;
        }

        if (!triggerValues.TryGetValue("MessageText", out var msgObj) || msgObj is not string msg)
        {
            return false;
        }

        if (filter.TryGetValue("CommandName", out var cmdName))
        {
            if (string.IsNullOrWhiteSpace(cmdName))
            {
                return false;
            }

            // Enforce strict word boundary check to prevent !so matching !sorry
            var exactMatch = msg.Equals(cmdName, StringComparison.OrdinalIgnoreCase);
            var prefixWithSpace = msg.StartsWith(cmdName + " ", StringComparison.OrdinalIgnoreCase);

            if (!exactMatch && !prefixWithSpace)
            {
                return false;
            }
        }

        if (filter.TryGetValue("MessageText", out var expectedMessage)
            && !string.Equals(msg, expectedMessage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.TryGetValue("Prefix", out var prefix))
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            if (!msg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
