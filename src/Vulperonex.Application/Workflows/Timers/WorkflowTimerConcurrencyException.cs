namespace Vulperonex.Application.Workflows.Timers;

public sealed class WorkflowTimerConcurrencyException(string timerId)
    : InvalidOperationException($"Workflow timer '{timerId}' was modified by another request.");
