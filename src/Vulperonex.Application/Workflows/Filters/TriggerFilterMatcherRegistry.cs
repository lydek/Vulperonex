using System.Collections.Frozen;
using Microsoft.Extensions.Logging;

namespace Vulperonex.Application.Workflows.Filters;

public sealed class TriggerFilterMatcherRegistry
{
    private readonly FrozenDictionary<string, ITriggerFilterMatcher> _matchers;
    private readonly ILogger<TriggerFilterMatcherRegistry> _logger;

    public TriggerFilterMatcherRegistry(
        IEnumerable<ITriggerFilterMatcher> matchers,
        ILogger<TriggerFilterMatcherRegistry> logger)
    {
        _matchers = matchers.ToFrozenDictionary(
            m => m.EventTypeKey,
            m => m,
            StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public bool TryMatch(
        string eventTypeKey,
        IReadOnlyDictionary<string, string> filter,
        IReadOnlyDictionary<string, object?> triggerValues,
        out bool isMatch)
    {
        if (_matchers.TryGetValue(eventTypeKey, out var matcher))
        {
            isMatch = matcher.Match(filter, triggerValues);
            return true;
        }

        isMatch = false;
        return false;
    }
}
