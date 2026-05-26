using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class ShoutoutActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => ShoutoutAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not ShoutoutAction shoutoutAction)
        {
            return ActionExecutionResult.Completed;
        }

        var targetLogin = templateResolver
            .Resolve(shoutoutAction.TargetLogin, context.ExpressionContext)
            .Trim()
            .TrimStart('@');
        if (string.IsNullOrWhiteSpace(targetLogin))
        {
            return ToOutput(new PlatformShoutoutResult(false, string.Empty, null, null));
        }

        return ToOutput(await helixClient.SendShoutoutAsync(targetLogin, cancellationToken));
    }

    private static ActionExecutionResult ToOutput(PlatformShoutoutResult result)
    {
        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsSent"] = result.IsSent,
                ["TargetLogin"] = result.TargetLogin,
                ["TargetUserId"] = result.TargetUserId ?? string.Empty,
                ["TargetDisplayName"] = result.TargetDisplayName ?? string.Empty,
            });
    }
}
