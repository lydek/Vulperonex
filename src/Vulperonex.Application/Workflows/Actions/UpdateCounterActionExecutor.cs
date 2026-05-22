using Vulperonex.Application.Counters;
using Vulperonex.Application.Expressions;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class UpdateCounterActionExecutor(
    ICounterRepository counterRepository,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
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
        var value = await counterRepository.IncrementAsync(key, updateCounterAction.Delta, cancellationToken);
        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Key"] = key,
                ["Value"] = value,
            });
    }
}
