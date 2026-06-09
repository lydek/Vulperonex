using Vulperonex.Application.Counters;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Settings;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class UpdateCounterActionExecutor(
    ICounterRepository counterRepository,
    ITemplateResolver templateResolver,
    ISystemSettingsService? settings = null) : IWorkflowActionExecutor
{
    public string ActionType => UpdateCounterAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not UpdateCounterAction updateCounterAction)
        {
            return ActionExecutionResult.Completed;
        }

        var key = templateResolver.Resolve(updateCounterAction.Key, context.ExpressionContext);

        // Simulation side-effect policy (§4.27): skip the real counter write unless persistent
        // writes are explicitly allowed. Return a synthetic value (delta as if from zero) so the
        // rest of the rule still runs.
        if (await SimulationSideEffect.ShouldSuppressPersistentWriteAsync(context, settings, cancellationToken))
        {
            return ActionExecutionResult.FromOutput(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Key"] = key,
                    ["Value"] = updateCounterAction.Delta,
                });
        }

        var value = await counterRepository.IncrementAsync(key, updateCounterAction.Delta, cancellationToken);
        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Key"] = key,
                ["Value"] = value,
            });
    }
}
