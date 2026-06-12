using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Dtos;

namespace Vulperonex.Infrastructure.Workflows;

/// <summary>
/// Scope-bridging <see cref="IWorkflowRuleQueryService"/> for singleton consumers
/// (the rule snapshot cache). The EF-backed query service is scoped because it
/// owns a DbContext; this bridge opens a short-lived scope per call so the
/// snapshot cache can live for the process lifetime and actually retain hits
/// across events.
/// </summary>
public sealed class ScopedWorkflowRuleQueryService(IServiceScopeFactory scopeFactory) : IWorkflowRuleQueryService
{
    public async Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(
        string eventTypeKey,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IWorkflowRuleQueryService>()
            .ListEnabledByEventTypeAsync(eventTypeKey, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkflowRuleSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IWorkflowRuleQueryService>()
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkflowRule?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IWorkflowRuleQueryService>()
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);
    }
}
