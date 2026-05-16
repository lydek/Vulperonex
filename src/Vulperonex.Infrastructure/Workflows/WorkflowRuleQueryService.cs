using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Workflows;

public sealed class WorkflowRuleQueryService(VulperonexDbContext context) : IWorkflowRuleQueryService
{
    public async Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(
        string eventTypeKey,
        CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowRules
            .AsNoTracking()
            .Where(rule => rule.IsEnabled && rule.EventTypeKey == eventTypeKey)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        return OrderRules(entities.Select(WorkflowRuleMapper.ToDomain)).ToArray();
    }

    public async Task<IReadOnlyList<WorkflowRuleSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context.WorkflowRules
            .AsNoTracking()
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        return entities
            .Select(WorkflowRuleMapper.ToSummary)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.CreatedAt)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<WorkflowRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules
            .AsNoTracking()
            .FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

        return entity is null ? null : WorkflowRuleMapper.ToDomain(entity);
    }

    private static IOrderedEnumerable<WorkflowRule> OrderRules(IEnumerable<WorkflowRule> rules)
    {
        return rules
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.CreatedAt)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal);
    }
}
