using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class TemplateRenderer
{
    public string Render(string template, IStreamEvent streamEvent)
    {
        return template
            .Replace("{user.displayName}", streamEvent.User?.DisplayName ?? string.Empty, StringComparison.Ordinal)
            .Replace("{user.id}", streamEvent.User?.UserId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{event.type}", streamEvent.EventTypeKey, StringComparison.Ordinal)
            .Replace("{event.platform}", streamEvent.Platform, StringComparison.Ordinal)
            .Replace("{event.message}", GetMessageText(streamEvent) ?? string.Empty, StringComparison.Ordinal);
    }

    private static string? GetMessageText(IStreamEvent streamEvent)
    {
        return streamEvent is UserSentMessageEvent messageEvent
            ? messageEvent.MessageText
            : null;
    }
}
