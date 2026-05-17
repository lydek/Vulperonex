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

    public async Task UpdateAsync(WorkflowRule rule, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules
            .FirstOrDefaultAsync(existing => existing.Id == rule.Id && existing.Version == expectedVersion, cancellationToken);

        if (entity is null)
        {
            if (await context.WorkflowRules.AnyAsync(existing => existing.Id == rule.Id, cancellationToken))
            {
                throw new WorkflowRuleConcurrencyException(rule.Id);
            }

            throw new KeyNotFoundException(rule.Id);
        }

        WorkflowRuleMapper.CopyToEntity(rule, entity);
        entity.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new WorkflowRuleConcurrencyException(rule.Id, ex);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException(id);

        context.WorkflowRules.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(string id, bool isEnabled, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var entity = await context.WorkflowRules
            .FirstOrDefaultAsync(rule => rule.Id == id && rule.Version == expectedVersion, cancellationToken);

        if (entity is null)
        {
            if (await context.WorkflowRules.AnyAsync(rule => rule.Id == id, cancellationToken))
            {
                throw new WorkflowRuleConcurrencyException(id);
            }

            throw new KeyNotFoundException(id);
        }

        entity.IsEnabled = isEnabled;
        entity.Version++;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new WorkflowRuleConcurrencyException(id, ex);
        }
    }
}
