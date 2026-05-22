using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;

namespace Vulperonex.Plugins.Abstractions;

public sealed class InvokePluginActionExecutor(IPluginRegistry pluginRegistry) : IWorkflowActionExecutor
{
    public string ActionType => InvokePluginAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not InvokePluginAction invokePlugin)
        {
            return ActionExecutionResult.Completed;
        }

        var plugin = pluginRegistry.Find(invokePlugin.PluginId);
        if (plugin is null)
        {
            return ActionExecutionResult.Completed;
        }

        var actionContext = new PluginActionContext(
            new ActionExecutionKey(
                context.StreamEvent.EventId,
                context.WorkflowRule.Id,
                context.ActionIndex,
                context.InvocationId),
            context.StreamEvent,
            context.WorkflowRule.Id,
            context.ActionIndex,
            context.StreamEvent.EventTypeKey,
            invokePlugin.Params);

        await plugin.ExecuteActionAsync(invokePlugin.ActionId, actionContext, cancellationToken);
        return ActionExecutionResult.Completed;
    }
}
