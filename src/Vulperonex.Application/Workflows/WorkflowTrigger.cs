namespace Vulperonex.Application.Workflows;

public sealed record WorkflowTrigger(
    string EventTypeKey,
    IReadOnlyDictionary<string, string>? Filter = null,
    string? MatchCondition = null)
{
    public IReadOnlyDictionary<string, string> Filter { get; init; } =
        Filter ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
