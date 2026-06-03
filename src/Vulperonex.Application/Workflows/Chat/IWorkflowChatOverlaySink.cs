namespace Vulperonex.Application.Workflows.Chat;

public interface IWorkflowChatOverlaySink
{
    Task PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default);

    Task PublishCheckInCardAsync(
        WorkflowCheckInCardOverlayMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowCheckInCardOverlayMessage(
    string DisplayName,
    string? AvatarUrl,
    int CheckInCount);
