namespace Vulperonex.Infrastructure.EventBus;

public sealed record ActionExecutionDecision(
    string DedupKey,
    string Status,
    int AttemptCount,
    bool ShouldExecute);
