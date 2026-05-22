using Vulperonex.Application.Expressions;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class TriggerEffectActionExecutor(
    IOverlayEffectEmitter effectEmitter,
    ITemplateResolver templateResolver,
    IClock clock) : IWorkflowActionExecutor
{
    public string ActionType => TriggerEffectAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not TriggerEffectAction triggerEffectAction)
        {
            return ActionExecutionResult.Completed;
        }

        var effectId = templateResolver.Resolve(triggerEffectAction.EffectId, context.ExpressionContext);
        var payload = new OverlayEffectPayload(
            1,
            context.StreamEvent.EventId,
            clock.UtcNow,
            effectId,
            triggerEffectAction.DurationMs);

        await effectEmitter.EmitAsync(payload, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EffectId"] = effectId,
                ["DurationMs"] = triggerEffectAction.DurationMs,
            });
    }
}
