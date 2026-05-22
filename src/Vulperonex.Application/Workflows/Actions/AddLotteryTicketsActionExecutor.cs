using Vulperonex.Application.Counters;
using Vulperonex.Application.Expressions;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class AddLotteryTicketsActionExecutor(
    ICounterRepository counterRepository,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
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

        var userId = templateResolver.Resolve(addLotteryTicketsAction.UserId, context.ExpressionContext);
        var key = $"lottery.tickets.{userId}";
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
