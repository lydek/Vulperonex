using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class LookupPlatformUserActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver,
    IPlatformUserResolver? resolver = null,
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

        // Single chat-known target (login or display name); fall back to the legacy fields for rules
        // saved before the single-Target redesign.
        var target = ResolveOptional(lookupAction.Target, context.ExpressionContext)
            ?? ResolveOptional(lookupAction.Login, context.ExpressionContext)
            ?? ResolveOptional(lookupAction.UserId, context.ExpressionContext);

        if (string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            // Simulated events must not hit the real Twitch API (§4.27 Simulation Side-Effect
            // Policy). Echo the requested target so downstream steps can still run.
            var simName = (target ?? string.Empty).TrimStart('@');
            logger?.LogInformation(
                "Platform user lookup (target='{Target}') skipped real Twitch call for simulated event.",
                target);
            return Output(simName, simName, simName, isFound: true);
        }

        if (resolver is not null)
        {
            var resolved = await resolver
                .ResolveAsync(context.StreamEvent.Platform, target ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            return Output(resolved.UserId ?? string.Empty, resolved.Login, resolved.DisplayName, resolved.IsFound);
        }

        // Legacy fallback (no resolver, e.g. unit tests): direct Helix by login/userId.
        var login = ResolveOptional(lookupAction.Login, context.ExpressionContext) ?? ResolveOptional(lookupAction.Target, context.ExpressionContext);
        var userId = ResolveOptional(lookupAction.UserId, context.ExpressionContext);
        var user = await helixClient.LookupUserAsync(login, userId, cancellationToken);
        return Output(
            user?.UserId ?? userId ?? string.Empty,
            user?.Login ?? login ?? string.Empty,
            user?.DisplayName ?? login ?? userId ?? string.Empty,
            user is not null,
            avatar: user?.Avatar ?? string.Empty,
            description: user?.Description ?? string.Empty,
            isAffiliate: user?.IsAffiliate ?? false);
    }

    private static ActionExecutionResult Output(
        string userId,
        string login,
        string displayName,
        bool isFound,
        string avatar = "",
        string description = "",
        bool isAffiliate = false)
    {
        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = userId,
                ["Login"] = login,
                ["DisplayName"] = displayName,
                ["Avatar"] = avatar,
                ["Description"] = description,
                ["IsAffiliate"] = isAffiliate,
                ["IsFound"] = isFound,
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
