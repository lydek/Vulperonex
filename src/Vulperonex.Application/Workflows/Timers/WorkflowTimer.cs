namespace Vulperonex.Application.Workflows.Timers;

public sealed record WorkflowTimer
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required int IntervalSeconds { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required DateTimeOffset NextFireAt { get; init; }
}
