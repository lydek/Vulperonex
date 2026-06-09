using Microsoft.Extensions.Logging;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Twitch;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class ShoutoutActionExecutor(
    IHelixClient helixClient,
    ITemplateResolver templateResolver,
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

        var targetLogin = templateResolver
            .Resolve(shoutoutAction.TargetLogin, context.ExpressionContext)
            .Trim()
            .TrimStart('@');
        if (string.IsNullOrWhiteSpace(targetLogin))
        {
            return ToOutput(new PlatformShoutoutResult(false, string.Empty, null, null));
        }

        // Simulated events must not cause real external side effects. A simulated raid/message
        // carries a fake target login, so hitting the real Helix shoutout endpoint would either
        // fail or — worse — fire a genuine shoutout for an unrelated real user. Skip the call and
        // return a synthetic success so the rest of the rule (e.g. the welcome chat message) still
        // exercises its happy path under simulation. Mirrors the Platform == "simulation" guards
        // in the throttle service and cooldown condition.
        if (string.Equals(context.StreamEvent.Platform, "simulation", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation(
                "Shoutout to '{TargetLogin}' skipped real Helix call for simulated event; returning synthetic success.",
                targetLogin);
            return ToOutput(new PlatformShoutoutResult(true, targetLogin, null, targetLogin));
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
            // Shoutout is best-effort. A Helix failure (Twitch not authorized, target login
            // not found, transient HTTP error) must not abort the rule and block later steps
            // such as a welcome chat message. Record the miss in output (IsSent=false) so
            // downstream steps can gate on {Step.<name>.IsSent}, and keep the pipeline alive.
            logger?.LogWarning(
                ex,
                "Shoutout to '{TargetLogin}' failed; continuing rule. Reason={Reason}",
                targetLogin,
                ex.Message);
            result = new PlatformShoutoutResult(false, targetLogin, null, null);
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
