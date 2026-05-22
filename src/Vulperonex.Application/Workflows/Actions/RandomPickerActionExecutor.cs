namespace Vulperonex.Application.Workflows.Actions;

public sealed class RandomPickerActionExecutor : IWorkflowActionExecutor
{
    public string ActionType => RandomPickerAction.ActionType;

    public Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action is not RandomPickerAction randomPicker || randomPicker.Choices.Count is 0)
        {
            return Task.FromResult(ActionExecutionResult.Completed);
        }

        var picked = Pick(randomPicker);
        return Task.FromResult(ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Picked"] = picked,
            }));
    }

    private static string Pick(RandomPickerAction action)
    {
        if (action.Weights is null || action.Weights.Count != action.Choices.Count)
        {
            return action.Choices[Random.Shared.Next(action.Choices.Count)];
        }

        var totalWeight = action.Weights.Where(weight => weight > 0).Sum();
        if (totalWeight <= 0)
        {
            return action.Choices[Random.Shared.Next(action.Choices.Count)];
        }

        var roll = Random.Shared.Next(1, totalWeight + 1);
        var accumulated = 0;
        for (var index = 0; index < action.Choices.Count; index++)
        {
            accumulated += Math.Max(0, action.Weights[index]);
            if (roll <= accumulated)
            {
                return action.Choices[index];
            }
        }

        return action.Choices[^1];
    }
}
