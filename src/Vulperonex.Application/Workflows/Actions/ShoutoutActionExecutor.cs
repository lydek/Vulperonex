using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class ShoutoutActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver,
    IPlatformUserResolver? resolver = null,
    ILogger<ShoutoutActionExecutor>? logger = null) : IWorkflowActionExecutor
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

        // Keep any leading '@' so the resolver can detect a display-name mention.
        var rawTarget = templateResolver
            .Resolve(shoutoutAction.TargetLogin, context.ExpressionContext)
            .Trim();
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return ToOutput(new PlatformShoutoutResult(false, string.Empty, null, null));
        }

        // Simulated events must not cause real external side effects. A simulated raid/message
        // carries a fake target, so hitting the real Helix shoutout endpoint would either fail or —
        // worse — fire a genuine shoutout for an unrelated real user. Skip and return a synthetic
        // success so the rest of the rule still exercises its happy path under simulation. Mirrors
        // the Platform == "simulation" guards in the throttle service and cooldown condition.
        if (string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            var simLogin = rawTarget.TrimStart('@');
            logger?.LogInformation(
                "Shoutout to '{Target}' skipped real Helix call for simulated event; returning synthetic success.",
                simLogin);
            return ToOutput(new PlatformShoutoutResult(true, simLogin, null, simLogin));
        }

        // Shoutout target MUST be a login. Resolve a chat-known login OR display name (e.g.
        // @DisplayName) to the exact login first; a display name or fuzzy input can never be passed
        // straight to the Helix shoutout endpoint.
        string targetLogin;
        string? resolvedUserId = null;
        string? resolvedDisplayName = null;
        if (resolver is not null)
        {
            var resolved = await resolver
                .ResolveAsync(context.StreamEvent.Platform, rawTarget, cancellationToken)
                .ConfigureAwait(false);
            if (!resolved.IsFound || string.IsNullOrWhiteSpace(resolved.Login))
            {
                logger?.LogWarning(
                    "Shoutout target '{Target}' could not be resolved to a login; skipping (best-effort).",
                    rawTarget);
                return ToOutput(new PlatformShoutoutResult(false, rawTarget.TrimStart('@'), null, null));
            }

            targetLogin = resolved.Login;
            resolvedUserId = resolved.UserId;
            resolvedDisplayName = resolved.DisplayName;
        }
        else
        {
            targetLogin = rawTarget.TrimStart('@');
        }

        PlatformShoutoutResult result;
        try
        {
            result = await helixClient.SendShoutoutAsync(targetLogin, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Shoutout is best-effort. A Helix failure (Twitch not authorized, transient HTTP
            // error) must not abort the rule and block later steps such as a welcome chat message.
            // Record the miss in output (IsSent=false) so downstream steps can gate on
            // {Step.<name>.IsSent}, and keep the pipeline alive.
            logger?.LogWarning(
                ex,
                "Shoutout to '{TargetLogin}' failed; continuing rule. Reason={Reason}",
                targetLogin,
                ex.Message);
            result = new PlatformShoutoutResult(false, targetLogin, resolvedUserId, resolvedDisplayName);
        }

        return ToOutput(result);
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
