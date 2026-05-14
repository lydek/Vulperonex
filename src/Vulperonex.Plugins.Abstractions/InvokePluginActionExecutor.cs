using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;

namespace Vulperonex.Plugins.Abstractions;

public sealed class InvokePluginActionExecutor(IPluginRegistry pluginRegistry) : IWorkflowActionExecutor
{
    public string ActionType => InvokePluginAction.ActionType;

    public async Task ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not InvokePluginAction invokePlugin)
        {
            return;
        }

        var plugin = pluginRegistry.Find(invokePlugin.PluginId);
        if (plugin is null)
        {
            return;
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
    }
}
