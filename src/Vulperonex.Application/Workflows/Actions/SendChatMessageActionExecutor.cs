using Vulperonex.Application.Expressions;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class SendChatMessageActionExecutor : IWorkflowActionExecutor
{
    private readonly IChatOutbox _chatOutbox;
    private readonly ITemplateResolver _templateResolver;
    private readonly TemplateRenderer? _legacyTemplateRenderer;

    public SendChatMessageActionExecutor(
        IChatOutbox chatOutbox,
        ITemplateResolver templateResolver,
        TemplateRenderer? legacyTemplateRenderer = null)
    {
        _chatOutbox = chatOutbox;
        _templateResolver = templateResolver;
        _legacyTemplateRenderer = legacyTemplateRenderer;
    }

    public SendChatMessageActionExecutor(
        IEnumerable<IPlatformChatSender> chatSenders,
        ITemplateResolver templateResolver,
        TemplateRenderer? legacyTemplateRenderer = null)
        : this(new DirectSendingChatOutbox(chatSenders), templateResolver, legacyTemplateRenderer)
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

        var platform = string.IsNullOrWhiteSpace(sendChatMessage.TargetPlatform)
            ? context.StreamEvent.Platform
            : Render(sendChatMessage.TargetPlatform, context);
        var message = Render(sendChatMessage.Template, context);
        var channel = RenderOptional(sendChatMessage.Channel, context);
        var dedupKey = string.IsNullOrWhiteSpace(sendChatMessage.DedupKey)
            ? $"action:{context.StreamEvent.EventId}:{context.WorkflowRule.Id}:{context.ActionIndex}"
            : Render(sendChatMessage.DedupKey, context);

        await _chatOutbox.EnqueueAsync(platform, channel, message, dedupKey, cancellationToken).ConfigureAwait(false);
        return ActionExecutionResult.Completed;
    }

    private string Render(string template, ActionExecutionContext context)
    {
        var rendered = _legacyTemplateRenderer is null
            ? template
            : _legacyTemplateRenderer.Render(template, context.StreamEvent);
        return _templateResolver.Resolve(rendered, context.ExpressionContext);
    }

    private string? RenderOptional(string? template, ActionExecutionContext context)
    {
        return string.IsNullOrWhiteSpace(template) ? null : Render(template, context);
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
