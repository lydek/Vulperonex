using Vulperonex.Application.Expressions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class ParseChatCommandActionExecutor(ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => ParseChatCommandAction.ActionType;

    public Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action is not ParseChatCommandAction parseAction)
        {
            return Task.FromResult(ActionExecutionResult.Completed);
        }

        var message = ResolveMessage(parseAction, context).Trim();
        var parsed = ChatCommandParser.Parse(message, ResolveOptional(parseAction.CommandPrefix, context.ExpressionContext));
        return Task.FromResult(ActionExecutionResult.FromOutput(parsed));
    }

    private string ResolveMessage(ParseChatCommandAction action, ActionExecutionContext context)
    {
        var configured = ResolveOptional(action.Message, context.ExpressionContext);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return context.StreamEvent is UserSentMessageEvent messageEvent
            ? messageEvent.MessageText
            : string.Empty;
    }

    private string? ResolveOptional(string? value, ExpressionContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var resolved = templateResolver.Resolve(value, context).Trim();
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }
}
