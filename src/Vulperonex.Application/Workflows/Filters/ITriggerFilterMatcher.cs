namespace Vulperonex.Application.Workflows.Filters;

public interface ITriggerFilterMatcher
{
    string EventTypeKey { get; }
    bool Match(IReadOnlyDictionary<string, string> filter, IReadOnlyDictionary<string, object?> triggerValues);
}
