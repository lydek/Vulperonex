namespace Vulperonex.Application.Workflows.Actions;

public sealed class SendChatMessageActionExecutor(
    IEnumerable<IPlatformChatSender> chatSenders,
    TemplateRenderer templateRenderer) : IWorkflowActionExecutor
{
    public string ActionType => SendChatMessageAction.ActionType;

    public async Task ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not SendChatMessageAction sendChatMessage)
        {
            return;
        }

        var platform = sendChatMessage.TargetPlatform ?? context.StreamEvent.Platform;
        var sender = chatSenders.FirstOrDefault(chatSender =>
            string.Equals(chatSender.Platform, platform, StringComparison.OrdinalIgnoreCase));

        if (sender is null)
        {
            return;
        }

        var message = templateRenderer.Render(sendChatMessage.Template, context.StreamEvent);
        await sender.SendAsync(message, cancellationToken);
    }
}
