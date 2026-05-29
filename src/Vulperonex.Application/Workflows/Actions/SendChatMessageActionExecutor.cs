using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class SendChatMessageActionExecutor : IWorkflowActionExecutor
{
    private readonly IChatOutbox _chatOutbox;
    private readonly TemplateRenderer _templateRenderer;

    public SendChatMessageActionExecutor(IChatOutbox chatOutbox, TemplateRenderer templateRenderer)
    {
        _chatOutbox = chatOutbox;
        _templateRenderer = templateRenderer;
    }

    public SendChatMessageActionExecutor(IEnumerable<IPlatformChatSender> chatSenders, TemplateRenderer templateRenderer)
        : this(new DirectSendingChatOutbox(chatSenders), templateRenderer)
    {
    }

    public string ActionType => SendChatMessageAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not SendChatMessageAction sendChatMessage)
        {
            return ActionExecutionResult.Completed;
        }

        var platform = sendChatMessage.TargetPlatform ?? context.StreamEvent.Platform;
        var message = _templateRenderer.Render(sendChatMessage.Template, context.StreamEvent);
        var channel = RenderOptional(sendChatMessage.Channel, context.StreamEvent);
        var dedupKey = string.IsNullOrWhiteSpace(sendChatMessage.DedupKey)
            ? $"action:{context.StreamEvent.EventId}:{context.WorkflowRule.Id}:{context.ActionIndex}"
            : _templateRenderer.Render(sendChatMessage.DedupKey, context.StreamEvent);

        await _chatOutbox.EnqueueAsync(platform, channel, message, dedupKey, cancellationToken).ConfigureAwait(false);
        return ActionExecutionResult.Completed;
    }

    private string? RenderOptional(string? template, IStreamEvent streamEvent)
    {
        return string.IsNullOrWhiteSpace(template) ? null : _templateRenderer.Render(template, streamEvent);
    }

    private sealed class DirectSendingChatOutbox(IEnumerable<IPlatformChatSender> chatSenders) : IChatOutbox
    {
        private readonly IReadOnlyList<IPlatformChatSender> _chatSenders = chatSenders.ToArray();

        public async Task<ChatOutboxEnqueueResult> EnqueueAsync(
            string platform,
            string? channel,
            string message,
            string? dedupKey = null,
            CancellationToken cancellationToken = default)
        {
            var sender = _chatSenders.FirstOrDefault(chatSender =>
                string.Equals(chatSender.Platform, platform, StringComparison.OrdinalIgnoreCase));

            if (sender is not null)
            {
                await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }

            var item = new ChatOutboxItem
            {
                Id = Guid.NewGuid(),
                Platform = platform,
                Channel = channel,
                Message = message,
                DedupKey = dedupKey,
                EnqueuedAt = DateTimeOffset.UtcNow,
                Status = sender is null ? ChatOutboxItemStatus.Skipped : ChatOutboxItemStatus.Sent,
            };

            return new ChatOutboxEnqueueResult(item);
        }

        public Task<IReadOnlyList<ChatOutboxItem>> SnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatOutboxItem>>([]);
        }

        public Task<IReadOnlyList<ChatOutboxItem>> DequeuePendingAsync(int maxItems, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatOutboxItem>>([]);
        }

        public Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkSkippedAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
