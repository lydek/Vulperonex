namespace Vulperonex.Application.Workflows.Actions;

public interface IPlatformChatSender
{
    string Platform { get; }

    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
