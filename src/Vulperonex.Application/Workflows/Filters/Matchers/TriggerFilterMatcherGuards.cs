using System.Collections.Generic;

namespace Vulperonex.Application.Workflows.Filters.Matchers;

internal static class TriggerFilterMatcherGuards
{
    public static bool ContainsOnlyKnownKeys(
        IReadOnlyDictionary<string, string> filter,
        IReadOnlySet<string> knownKeys)
    {
        foreach (var key in filter.Keys)
        {
            if (!knownKeys.Contains(key))
            {
                return false;
            }
        }

        return true;
    }
}
