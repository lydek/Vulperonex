using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Workflows;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Workflows;

public sealed class WorkflowRuleRepository(VulperonexDbContext context) : IWorkflowRuleRepository
{
    public async Task AddAsync(WorkflowRule rule, CancellationToken cancellationToken = default)
    {
        context.WorkflowRules.Add(WorkflowRuleMapper.ToEntity(rule));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkflowRule rule, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules.FirstOrDefaultAsync(existing => existing.Id == rule.Id, cancellationToken)
            ?? throw new KeyNotFoundException(rule.Id);

        WorkflowRuleMapper.CopyToEntity(rule, entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(id);

        context.WorkflowRules.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(string id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(id);

        entity.IsEnabled = isEnabled;
        await context.SaveChangesAsync(cancellationToken);
    }
}
