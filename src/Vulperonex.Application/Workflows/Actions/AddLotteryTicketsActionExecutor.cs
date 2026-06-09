using Vulperonex.Application.Counters;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class AddLotteryTicketsActionExecutor(
    ICounterRepository counterRepository,
    ITemplateResolver templateResolver,
    IModuleStateService moduleStateService,
    ISystemSettingsService? settings = null) : IWorkflowActionExecutor
{
    public string ActionType => AddLotteryTicketsAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not AddLotteryTicketsAction addLotteryTicketsAction)
        {
            return ActionExecutionResult.Completed;
        }

        if (!await moduleStateService.IsEnabledAsync("lottery", cancellationToken).ConfigureAwait(false))
        {
            throw new DependencyMissingException("Lottery Module is disabled.");
        }

        var userId = templateResolver.Resolve(addLotteryTicketsAction.UserId, context.ExpressionContext);
        var key = $"lottery.tickets.{userId}";

        // Simulation side-effect policy (§4.27): skip the real ticket write unless persistent
        // writes are explicitly allowed. Return a synthetic ticket count so the rule still runs.
        if (await SimulationSideEffect.ShouldSuppressPersistentWriteAsync(context, settings, cancellationToken))
        {
            return ActionExecutionResult.FromOutput(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Key"] = key,
                    ["UserId"] = userId,
                    ["TicketsAdded"] = addLotteryTicketsAction.Amount,
                    ["TicketCount"] = addLotteryTicketsAction.Amount,
                });
        }

        var value = await counterRepository.IncrementAsync(key, addLotteryTicketsAction.Amount, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Key"] = key,
                ["UserId"] = userId,
                ["TicketsAdded"] = addLotteryTicketsAction.Amount,
                ["TicketCount"] = value,
            });
    }
}
