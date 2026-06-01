using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Domain;

namespace Vulperonex.Web;

/// <summary>
/// Seeds 7 default typical typed workflow rules on first startup (when database is empty)
/// so that out-of-the-box installs immediately have working stream automation baseline examples.
///
/// Behaviour:
///   - Runs once per process start
///   - Skips if any existing rules are present in the database (idempotent, operator-managed)
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

            var existingRules = await queries.ListAsync(cancellationToken);
            if (existingRules.Count > 0)
            {
                return; // Database already contains rules, skip seeding to protect operator configurations
            }

            var seeds = BuildSampleRules();
            foreach (var seed in seeds)
            {
                await repository.AddAsync(seed, cancellationToken);
            }

            logger.LogInformation(
                "Seeded {Count} default typical typed workflow rules so stream automation works out of the box. Operator can edit or disable them freely.",
                seeds.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to seed default workflow rules; startup continues without them.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static List<WorkflowRule> BuildSampleRules()
    {
        return new List<WorkflowRule>
        {
            // 1. !checkin 打卡
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — !checkin chat command",
                EventTypeKey = "user.message",
                IsEnabled = true,
                Priority = 100,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CommandName"] = "!checkin"
                    }
                },
                Actions =
                [
                    new TriggerCheckInAction
                    {
                        UserId = "{Member.UserId}",
                        // Platform intentionally left null: executor falls back to
                        // context.StreamEvent.Platform when blank. Avoids forcing
                        // operators to fill a redundant field.
                        OutputVariable = "CheckIn"
                    },
                    new SendChatMessageAction
                    {
                        Template = "@{Member.DisplayName} 成功簽到！已簽到 {Step.CheckIn.CheckInCount} 次，累積 Loyalty: {Step.CheckIn.TotalLoyalty}"
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
            },

            // 2. !so 喊話 (mod-only)
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — Shoutout moderator-only command",
                EventTypeKey = "user.message",
                IsEnabled = true,
                Priority = 200,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CommandName"] = "!so"
                    }
                },
                Conditions =
                [
                    new UserRoleCondition
                    {
                        Roles = StreamRole.Moderator,
                        Mode = UserRoleMatchMode.HasAny
                    }
                ],
                Actions =
                [
                    new ShoutoutAction
                    {
                        TargetLogin = "{Trigger.MessageText.Substring(4)}"
                    },
                    new SendChatMessageAction
                    {
                        Template = "大家快去追隨優質實況主 @{Trigger.MessageText.Substring(4)} ！"
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 1,
                    CooldownSeconds = 10
                },
                TimeoutSeconds = 30
            },

            // 3. Bits 100+ 特效
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — Bits 100+ special alert",
                EventTypeKey = "user.donated",
                IsEnabled = true,
                Priority = 300,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["MinAmount"] = "100"
                    }
                },
                Actions =
                [
                    new TriggerEffectAction
                    {
                        EffectId = "celebration",
                        DurationMs = 5000
                    },
                    new EmitOverlayWidgetAction
                    {
                        WidgetType = "alert",
                        DisplayText = "感謝 @{Trigger.UserDisplayName} 贊助了 {Trigger.Amount} Bits！",
                        Severity = "info",
                        DurationMs = 5000
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 5,
                    CooldownSeconds = 2
                },
                TimeoutSeconds = 30
            },

            // 4. 新訂閱歡迎
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — New subscription welcome",
                EventTypeKey = "user.subscribed",
                IsEnabled = true,
                Priority = 400,
                Trigger = new WorkflowTrigger(),
                Actions =
                [
                    new SendChatMessageAction
                    {
                        Template = "歡迎 @{Trigger.UserDisplayName} 加入訂閱大家庭！感謝你的支持！"
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 5,
                    CooldownSeconds = 1
                },
                TimeoutSeconds = 30
            },

            // 5. 50+ gifted sub 警報
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — Mega gifted sub 50+ alert",
                EventTypeKey = "user.gifted_sub",
                IsEnabled = true,
                Priority = 500,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["MinGiftCount"] = "50"
                    }
                },
                Actions =
                [
                    new EmitOverlayWidgetAction
                    {
                        WidgetType = "alert",
                        DisplayText = "🚨 哇！感謝 @{Trigger.UserDisplayName} 大氣贈送了 {Trigger.GiftCount} 個訂閱！",
                        Severity = "warning",
                        DurationMs = 10000
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 1,
                    CooldownSeconds = 10
                },
                TimeoutSeconds = 30
            },

            // 6. Raid 歡迎
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — Incoming raid welcome",
                EventTypeKey = "channel.raided",
                IsEnabled = true,
                Priority = 600,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["MinViewers"] = "5"
                    }
                },
                Actions =
                [
                    new ShoutoutAction
                    {
                        TargetLogin = "{Trigger.UserDisplayName}"
                    },
                    new SendChatMessageAction
                    {
                        Template = "感謝 @{Trigger.UserDisplayName} 帶了 {Trigger.ViewerCount} 位小夥伴來 Raid！大家快去關注他！"
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 1,
                    CooldownSeconds = 30
                },
                TimeoutSeconds = 30
            },

            // 7. 抽獎 reward 兌換
            new()
            {
                Id = UlidGenerator.NewUlidString(),
                Name = "Default — Lottery Ticket point redemption",
                EventTypeKey = "reward.redeemed",
                IsEnabled = true,
                Priority = 700,
                Trigger = new WorkflowTrigger
                {
                    Filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["RewardName"] = "Lottery Ticket"
                    }
                },
                Actions =
                [
                    new AddLotteryTicketsAction
                    {
                        UserId = "{Member.UserId}",
                        Amount = 1
                    },
                    new SendChatMessageAction
                    {
                        Template = "@{Trigger.UserDisplayName} 成功兌換了一張抽獎券！祝你中大獎！"
                    }
                ],
                Throttle = new WorkflowThrottlePolicy
                {
                    MaxConcurrent = 2,
                    CooldownSeconds = 5
                },
                TimeoutSeconds = 30
            }
        };
    }
}
