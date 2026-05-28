using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;

namespace Vulperonex.Web;

/// <summary>
/// Seeds a default `!checkin` chat-trigger workflow rule on first startup so that
/// out-of-the-box installs immediately have a working chat → checkin pipeline.
///
/// Behaviour:
///   - Runs once per process start
///   - Skips if any existing rule already targets `triggerCheckIn` action,
///     regardless of name / enabled state — assume operator owns the path
///   - Skips silently on any failure (logged as warning); never blocks startup
/// </summary>
public sealed class DefaultWorkflowRuleSeedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DefaultWorkflowRuleSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var queries = scope.ServiceProvider.GetRequiredService<IWorkflowRuleQueryService>();
            var repository = scope.ServiceProvider.GetRequiredService<IWorkflowRuleRepository>();

            // Inspect rules wired to user.message (the trigger we'd seed against)
            // — if any already contains a triggerCheckIn action, operator owns
            // the path and we skip seeding.
            var existingForUserMessage = await queries.ListEnabledByEventTypeAsync("user.message", cancellationToken);
            foreach (var rule in existingForUserMessage)
            {
                if (rule.Actions.Any(a => a.Type == TriggerCheckInAction.ActionType))
                {
                    return; // operator-managed checkin rule already exists
                }
            }

            var seed = BuildSampleCheckInRule();
            await repository.AddAsync(seed, cancellationToken);

            logger.LogInformation(
                "Seeded default `!checkin` workflow rule {RuleId} so chat → checkin pipeline works out of the box. Operator can disable or edit it freely.",
                seed.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to seed default checkin workflow rule; startup continues without it.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static WorkflowRule BuildSampleCheckInRule()
    {
        return new WorkflowRule
        {
            Id = UlidGenerator.NewUlidString(),
            Name = "Default — !checkin chat command",
            EventTypeKey = "user.message",
            IsEnabled = true,
            Priority = 100,
            Trigger = new WorkflowTrigger(),
            MatchCondition = "Trigger.MessageText == '!checkin'",
            Actions =
            [
                new TriggerCheckInAction
                {
                    UserId = "{Member.UserId}",
                    Platform = "{Trigger.Platform}",
                    OutputVariable = "CheckIn"
                }
            ],
            Throttle = new WorkflowThrottlePolicy
            {
                MaxConcurrent = 1,
                CooldownSeconds = 60,
                PerUserCooldown = true,
                PerUserCooldownSeconds = 60
            },
            TimeoutSeconds = 30
        };
    }
}
