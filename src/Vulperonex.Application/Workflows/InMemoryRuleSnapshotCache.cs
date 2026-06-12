using System.Collections.Concurrent;

namespace Vulperonex.Application.Workflows;

public sealed class InMemoryRuleSnapshotCache(IWorkflowRuleQueryService queryService) : IRuleSnapshotCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<WorkflowRule>> _rulesByEventType = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WorkflowRule> _rulesById = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<WorkflowRule>> GetByEventTypeAsync(
        string eventTypeKey,
        CancellationToken cancellationToken = default)
    {
        if (_rulesByEventType.TryGetValue(eventTypeKey, out var cachedRules))
        {
            return Clone(cachedRules);
        }

        var rules = await queryService.ListEnabledByEventTypeAsync(eventTypeKey, cancellationToken);
        var snapshot = Clone(rules);
        _rulesByEventType[eventTypeKey] = snapshot;
        foreach (var rule in snapshot)
        {
            // Internal entries can share instances with the stored snapshot;
            // callers only ever receive clones, so no extra copy is needed here.
            _rulesById[rule.Id] = rule;
        }

        return Clone(snapshot);
    }

    public async Task<WorkflowRule?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_rulesById.TryGetValue(id, out var cachedRule))
        {
            return Clone(cachedRule);
        }

        var rule = await queryService.GetAsync(id, cancellationToken);
        if (rule is null)
        {
            return null;
        }

        var snapshot = Clone(rule);
        _rulesById[id] = snapshot;
        return Clone(snapshot);
    }

    public void Invalidate(string? ruleId = null)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            _rulesByEventType.Clear();
            _rulesById.Clear();
            return;
        }

        _rulesById.TryRemove(ruleId, out _);
        _rulesByEventType.Clear();
    }

    private static IReadOnlyList<WorkflowRule> Clone(IEnumerable<WorkflowRule> rules)
    {
        return rules.Select(Clone).ToArray();
    }

    private static WorkflowRule Clone(WorkflowRule rule)
    {
        return rule with
        {
            Conditions = rule.Conditions.ToArray(),
            Actions = rule.Actions.ToArray(),
            OnFailureSteps = rule.OnFailureSteps.ToArray(),
        };
    }
}
