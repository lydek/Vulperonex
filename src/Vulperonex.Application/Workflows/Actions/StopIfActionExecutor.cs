using Vulperonex.Application.Expressions;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class StopIfActionExecutor(IExpressionEvaluator expressionEvaluator) : IWorkflowActionExecutor
{
    public string ActionType => StopIfAction.ActionType;

    public Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action is StopIfAction stopIfAction
            && CoerceToBoolean(expressionEvaluator.Evaluate(stopIfAction.Condition, context.ExpressionContext)))
        {
            throw new WorkflowGracefulStopException($"StopIf condition matched: {stopIfAction.Condition}");
        }

        return Task.FromResult(ActionExecutionResult.Completed);
    }

    private static bool CoerceToBoolean(object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            float number => Math.Abs(number) > float.Epsilon,
            double number => Math.Abs(number) > double.Epsilon,
            decimal number => number != 0,
            string text when bool.TryParse(text, out var boolean) => boolean,
            _ => false,
        };
    }
}
