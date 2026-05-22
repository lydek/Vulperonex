namespace Vulperonex.Application.Workflows.Actions;

public sealed record ActionExecutionResult(
    IReadOnlyDictionary<string, object?>? OutputValues = null,
    bool IsSkipped = false)
{
    public static ActionExecutionResult Completed { get; } = new();

    public static ActionExecutionResult Skipped { get; } = new(IsSkipped: true);

    public static ActionExecutionResult FromOutput(IReadOnlyDictionary<string, object?> outputValues)
    {
        ArgumentNullException.ThrowIfNull(outputValues);
        return new ActionExecutionResult(outputValues);
    }
}
