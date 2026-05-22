using Vulperonex.Application.EventBus;
using Vulperonex.Application.Expressions;
using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class EmitSystemEventActionExecutor(
    IStreamEventBus eventBus,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => EmitSystemEventAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not EmitSystemEventAction emitSystemEventAction)
        {
            return ActionExecutionResult.Completed;
        }

        var payload = emitSystemEventAction.Payload.ToDictionary(
            pair => pair.Key,
            pair => templateResolver.Resolve(pair.Value, context.ExpressionContext),
            StringComparer.OrdinalIgnoreCase);
        var depth = context.StreamEvent is WorkflowSystemEvent systemEvent ? systemEvent.Depth + 1 : 1;

        var emitted = new WorkflowSystemEvent
        {
            EventTypeKey = templateResolver.Resolve(emitSystemEventAction.EventTypeKey, context.ExpressionContext),
            Platform = context.StreamEvent.Platform,
            User = context.StreamEvent.User,
            Depth = depth,
            Payload = payload,
        };

        await eventBus.PublishAsync(emitted, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EventId"] = emitted.EventId,
                ["EventTypeKey"] = emitted.EventTypeKey,
                ["Depth"] = emitted.Depth,
            });
    }
}
