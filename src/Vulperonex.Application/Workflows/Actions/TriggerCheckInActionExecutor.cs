using System.Text.Json;
using Vulperonex.Application.Data;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Expressions;
using Vulperonex.Application.Members;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;

namespace Vulperonex.Application.Workflows.Actions;

public sealed class TriggerCheckInActionExecutor(
    IMemberStreamStateRepository streamStateRepository,
    ITemplateResolver templateResolver,
    IStreamEventBus eventBus,
    IModuleStateService moduleStateService,
    ISystemSettingsService systemSettingsService,
    IPlatformUserDisplayInfoProvider userInfoProvider,
    IMemberQueryService memberQueryService,
    IMemberAuditLogRepository auditLogRepository,
    ITransactionProvider transactionProvider) : IWorkflowActionExecutor
{
    public string ActionType => TriggerCheckInAction.ActionType;

    public async Task<ActionExecutionResult> ExecuteAsync(
        WorkflowAction action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (action is not TriggerCheckInAction triggerCheckInAction)
        {
            return ActionExecutionResult.Completed;
        }

        if (!await moduleStateService.IsEnabledAsync("checkin", cancellationToken).ConfigureAwait(false))
        {
            throw new DependencyMissingException("Check-In Module is disabled.");
        }

        var userId = templateResolver.Resolve(triggerCheckInAction.UserId, context.ExpressionContext);
        var platform = string.IsNullOrWhiteSpace(triggerCheckInAction.Platform)
            ? context.StreamEvent.Platform
            : templateResolver.Resolve(triggerCheckInAction.Platform, context.ExpressionContext);

        var identity = PlatformIdentity.Create(platform, userId);

        var memberBefore = await memberQueryService.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Member '{platform}:{userId}' was not found before check-in.");

        var beforeSnapshot = JsonSerializer.Serialize(new
        {
            totalLoyalty = memberBefore.Loyalty.TotalLoyalty,
            checkInCount = memberBefore.Loyalty.CheckInCount,
        });

        var count = 0;
        MemberReadModel? memberAfter = null;

        await using var transaction = await transactionProvider.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            count = await streamStateRepository.IncrementCheckInAsync(identity, cancellationToken).ConfigureAwait(false);
            memberAfter = await memberQueryService.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Member '{platform}:{userId}' was not found after check-in increment.");

            var afterSnapshot = JsonSerializer.Serialize(new
            {
                totalLoyalty = memberAfter.Loyalty.TotalLoyalty,
                checkInCount = memberAfter.Loyalty.CheckInCount,
            });

            await auditLogRepository.AppendAsync(new MemberAuditLog
            {
                MemberId = memberAfter.MemberId,
                ActorKind = "workflow",
                ActorId = context.WorkflowRule.Id,
                Operation = "checkin",
                BeforeJson = beforeSnapshot,
                AfterJson = afterSnapshot,
                Reason = $"Workflow rule '{context.WorkflowRule.Id}' automatically incremented check-in count.",
                OccurredAt = DateTimeOffset.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var stampsPerRound = await systemSettingsService.GetAsync<int>("overlay.member.stamps_per_round", 10, cancellationToken);
        if (stampsPerRound <= 0)
        {
            stampsPerRound = 10;
        }

        var roundIndex = (int)Math.Ceiling((double)count / stampsPerRound);
        var stampSlotInRound = ((count - 1) % stampsPerRound) + 1;

        var displayName = userId;
        string? avatarUrl = null;
        var streamRole = StreamRole.None;

        var displayInfo = await userInfoProvider.GetAsync(platform, userId, cancellationToken);
        if (displayInfo != null)
        {
            displayName = displayInfo.DisplayName ?? userId;
            avatarUrl = displayInfo.AvatarUrl;
            if (displayInfo.IsSubscriber)
            {
                streamRole |= StreamRole.Subscriber;
            }
        }

        if (context.StreamEvent?.User?.UserId == userId)
        {
            displayName = context.StreamEvent.User.DisplayName;
            streamRole = context.StreamEvent.User.Roles;
        }

        var streamUser = new StreamUser(platform, userId, displayName, streamRole);
        var checkInEvent = new MemberCheckedInEvent
        {
            Platform = platform,
            User = streamUser,
            AvatarUrl = avatarUrl,
            CheckInCount = count,
            TotalLoyalty = memberAfter.Loyalty.TotalLoyalty,
            RoundIndex = roundIndex,
            StampSlotInRound = stampSlotInRound
        };

        await eventBus.PublishAsync(checkInEvent, cancellationToken);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Platform"] = platform,
                ["UserId"] = userId,
                ["CheckInCount"] = count,
            });
    }
}
