using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class LookupPlatformUserActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver,
    ILogger<LookupPlatformUserActionExecutor>? logger = null) : IWorkflowActionExecutor
{
    public string ActionType => LookupPlatformUserAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not LookupPlatformUserAction lookupAction)
        {
            return ActionExecutionResult.Completed;
        }

        var login = ResolveOptional(lookupAction.Login, context.ExpressionContext);
        var userId = ResolveOptional(lookupAction.UserId, context.ExpressionContext);

        if (string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            // Simulated events must not hit the real Twitch API (§4.27 Simulation Side-Effect
            // Policy). Return a synthetic lookup echoing the requested identifiers so downstream
            // steps can still run; real profile fields (avatar/description) are unavailable.
            logger?.LogInformation(
                "Platform user lookup (login='{Login}', userId='{UserId}') skipped real Helix call for simulated event.",
                login,
                userId);
            return ActionExecutionResult.FromOutput(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UserId"] = userId ?? string.Empty,
                    ["Login"] = login ?? string.Empty,
                    ["DisplayName"] = login ?? userId ?? string.Empty,
                    ["Avatar"] = string.Empty,
                    ["Description"] = string.Empty,
                    ["IsAffiliate"] = false,
                    ["IsFound"] = true,
                });
        }

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
