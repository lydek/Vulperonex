namespace Vulperonex.Application.Workflows;

public sealed class WorkflowGracefulStopException : Exception
{
    public WorkflowGracefulStopException(string? message = null)
        : base(message ?? "Workflow stopped gracefully.")
    {
    }
}
