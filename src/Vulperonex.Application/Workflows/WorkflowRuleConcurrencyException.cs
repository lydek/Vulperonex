namespace Vulperonex.Application.Workflows;

public sealed class WorkflowRuleConcurrencyException(string id, Exception? innerException = null) : InvalidOperationException(
    $"Workflow rule '{id}' was modified by another request.",
    innerException);
