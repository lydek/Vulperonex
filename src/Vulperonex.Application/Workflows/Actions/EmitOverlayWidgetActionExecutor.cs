using Vulperonex.Application.Expressions;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class EmitOverlayWidgetActionExecutor(
    IOverlayWidgetEmitter widgetEmitter,
    ITemplateResolver templateResolver,
    IClock clock) : IWorkflowActionExecutor
{
    public string ActionType => EmitOverlayWidgetAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not EmitOverlayWidgetAction emitOverlayWidgetAction)
        {
            return ActionExecutionResult.Completed;
        }

        var widgetType = templateResolver.Resolve(emitOverlayWidgetAction.WidgetType, context.ExpressionContext);
        var overlayTarget = templateResolver.Resolve(emitOverlayWidgetAction.OverlayTarget, context.ExpressionContext);
        var displayText = templateResolver.Resolve(emitOverlayWidgetAction.DisplayText, context.ExpressionContext);
        var severity = templateResolver.Resolve(emitOverlayWidgetAction.Severity, context.ExpressionContext);

        var payload = new OverlayWidgetPayload(
            1,
            context.StreamEvent.EventId,
            clock.UtcNow,
            widgetType,
            overlayTarget,
            displayText,
            severity,
            emitOverlayWidgetAction.DurationMs);

        await widgetEmitter.EmitAsync(payload, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["WidgetType"] = widgetType,
                ["OverlayTarget"] = overlayTarget,
                ["DisplayText"] = displayText,
                ["Severity"] = severity,
                ["DurationMs"] = emitOverlayWidgetAction.DurationMs,
            });
    }
}
