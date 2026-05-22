using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class LookupTwitchUserActionExecutor(
    ITwitchHelixClient helixClient,
    ITemplateResolver templateResolver) : IWorkflowActionExecutor
{
    public string ActionType => LookupTwitchUserAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not LookupTwitchUserAction lookupAction)
        {
            return ActionExecutionResult.Completed;
        }

        var login = ResolveOptional(lookupAction.Login, context.ExpressionContext);
        var userId = ResolveOptional(lookupAction.UserId, context.ExpressionContext);
        var user = await helixClient.LookupUserAsync(login, userId, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = user?.UserId ?? userId ?? string.Empty,
                ["Login"] = user?.Login ?? login ?? string.Empty,
                ["DisplayName"] = user?.DisplayName ?? login ?? userId ?? string.Empty,
                ["Avatar"] = user?.Avatar ?? string.Empty,
                ["Description"] = user?.Description ?? string.Empty,
                ["IsAffiliate"] = user?.IsAffiliate ?? false,
                ["IsFound"] = user is not null,
            });
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
