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

        // Simulation side-effect policy (§4.27): unless persistent writes are explicitly allowed,
        // a simulated event must not increment the real check-in count or write an audit log. Emit
        // a read-only synthetic card (mirrors the SimulateEndpoints check-in isTest path) so the
        // overlay preview still reacts.
        if (await SimulationSideEffect.ShouldSuppressPersistentWriteAsync(context, systemSettingsService, cancellationToken))
        {
            return await SimulateCheckInAsync(identity, platform, userId, context, cancellationToken).ConfigureAwait(false);
        }

        var memberBefore = await memberQueryService.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Member '{platform}:{userId}' was not found before check-in.");
        var resetTimeLocal = await systemSettingsService
            .GetAsync(SystemSettingKey.CheckInResetTimeLocal, "05:00", cancellationToken)
            .ConfigureAwait(false);
        var currentWindowStartedAt = ResolveWindowStartUtc(context.StreamEvent.OccurredAt, resetTimeLocal);
        var recentLogs = await auditLogRepository
            .QueryAsync(memberBefore.MemberId, limit: 20, offset: 0, cancellationToken)
            .ConfigureAwait(false);
        var latestCheckInLog = recentLogs
            .Where(log => string.Equals(log.Operation, "checkin", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(log => log.OccurredAt)
            .FirstOrDefault();
        var shouldEmitRepeatCard = await systemSettingsService
            .GetAsync(SystemSettingKey.CheckInRepeatCardEnabled, true, cancellationToken)
            .ConfigureAwait(false);

        var stampsPerRound = await systemSettingsService.GetAsync<int>("overlay.member.stamps_per_round", 10, cancellationToken);
        if (stampsPerRound <= 0)
        {
            stampsPerRound = 10;
        }

        // Short-circuit BEFORE incrementing the check-in count when the member
        // has already checked in within the current reset window. Without this
        // gate, repeat triggers would keep advancing the stamp board.
        var isRepeat = latestCheckInLog is not null && latestCheckInLog.OccurredAt >= currentWindowStartedAt;
        if (isRepeat)
        {
            var repeatedRoundIndex = (int)Math.Ceiling((double)Math.Max(1, memberBefore.Loyalty.CheckInCount) / stampsPerRound);
            var repeatedStampSlot = memberBefore.Loyalty.CheckInCount <= 0
                ? 0
                : ((memberBefore.Loyalty.CheckInCount - 1) % stampsPerRound) + 1;
            var repeatedDisplayName = context.StreamEvent?.User?.UserId == userId
                ? context.StreamEvent.User.DisplayName
                : memberBefore.Identities
                    .FirstOrDefault(existingIdentity => existingIdentity.Platform == platform && existingIdentity.PlatformUserId == userId)?
                    .DisplayName ?? userId;

            string? repeatedAvatarUrl = null;
            var repeatedDisplayInfo = await userInfoProvider.GetAsync(platform, userId, cancellationToken).ConfigureAwait(false);
            if (repeatedDisplayInfo != null)
            {
                repeatedDisplayName = repeatedDisplayInfo.DisplayName ?? repeatedDisplayName;
                repeatedAvatarUrl = repeatedDisplayInfo.AvatarUrl;
            }

            var repeatedLogin = ResolveLogin(context, userId, repeatedDisplayInfo?.Login);

            if (shouldEmitRepeatCard)
            {
                await eventBus.PublishAsync(new MemberCheckedInEvent
                {
                    Platform = platform,
                    User = new StreamUser(platform, userId, repeatedDisplayName, context.StreamEvent?.User?.Roles ?? StreamRole.None, repeatedLogin),
                    AvatarUrl = repeatedAvatarUrl,
                    CheckInCount = memberBefore.Loyalty.CheckInCount,
                    TotalLoyalty = memberBefore.Loyalty.TotalLoyalty,
                    RoundIndex = repeatedRoundIndex,
                    StampSlotInRound = repeatedStampSlot
                }, cancellationToken).ConfigureAwait(false);
            }

            return ActionExecutionResult.FromOutput(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Status"] = "repeat",
                    ["Platform"] = platform,
                    ["UserId"] = userId,
                    ["DisplayName"] = repeatedDisplayName,
                    ["CheckInCount"] = memberBefore.Loyalty.CheckInCount,
                    ["TotalLoyalty"] = memberBefore.Loyalty.TotalLoyalty,
                    ["RoundIndex"] = repeatedRoundIndex,
                    ["StampSlotInRound"] = repeatedStampSlot,
                    ["WindowStartedAt"] = currentWindowStartedAt,
                });
        }

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
                OccurredAt = context.StreamEvent.OccurredAt,
            }, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
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

        var login = ResolveLogin(context, userId, displayInfo?.Login);
        var streamUser = new StreamUser(platform, userId, displayName, streamRole, login);
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
                ["DisplayName"] = displayName,
                ["CheckInCount"] = count,
                ["TotalLoyalty"] = memberAfter.Loyalty.TotalLoyalty,
                ["RoundIndex"] = roundIndex,
                ["StampSlotInRound"] = stampSlotInRound,
                ["WindowStartedAt"] = currentWindowStartedAt,
            });
    }

    private async Task<ActionExecutionResult> SimulateCheckInAsync(
        PlatformIdentity identity,
        string platform,
        string userId,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Read-only: do NOT increment or write an audit log. Synthesise the next count from any
        // existing record so the overlay card preview advances without mutating real data.
        var existing = await memberQueryService.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
        var baseCount = existing?.Loyalty.CheckInCount ?? 0;
        var count = baseCount + 1;
        var totalLoyalty = existing?.Loyalty.TotalLoyalty ?? 0;

        var stampsPerRound = await systemSettingsService.GetAsync<int>("overlay.member.stamps_per_round", 10, cancellationToken).ConfigureAwait(false);
        if (stampsPerRound <= 0)
        {
            stampsPerRound = 10;
        }

        var roundIndex = (int)Math.Ceiling((double)count / stampsPerRound);
        var stampSlotInRound = ((count - 1) % stampsPerRound) + 1;

        var displayName = userId;
        string? avatarUrl = null;
        var streamRole = StreamRole.None;

        var displayInfo = await userInfoProvider.GetAsync(platform, userId, cancellationToken).ConfigureAwait(false);
        if (displayInfo != null)
        {
            displayName = displayInfo.DisplayName ?? userId;
            avatarUrl = displayInfo.AvatarUrl;
            if (displayInfo.IsSubscriber)
            {
                streamRole |= StreamRole.Subscriber;
            }
        }

        if (context.StreamEvent.User is { } eventUser && eventUser.UserId == userId)
        {
            displayName = eventUser.DisplayName;
            streamRole = eventUser.Roles;
        }

        var login = ResolveLogin(context, userId, displayInfo?.Login);
        var streamUser = new StreamUser(platform, userId, displayName, streamRole, login);
        await eventBus.PublishAsync(new MemberCheckedInEvent
        {
            Platform = platform,
            User = streamUser,
            AvatarUrl = avatarUrl,
            CheckInCount = count,
            TotalLoyalty = totalLoyalty,
            RoundIndex = roundIndex,
            StampSlotInRound = stampSlotInRound,
        }, cancellationToken).ConfigureAwait(false);

        return ActionExecutionResult.FromOutput(
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Platform"] = platform,
                ["UserId"] = userId,
                ["DisplayName"] = displayName,
                ["CheckInCount"] = count,
                ["TotalLoyalty"] = totalLoyalty,
                ["RoundIndex"] = roundIndex,
                ["StampSlotInRound"] = stampSlotInRound,
            });
    }

    // Carry the real platform login onto the re-emitted member event so downstream rules can do
    // strict {Member.Login} lookups. Prefer the triggering event's login (when it is the same
    // user); otherwise fall back to the value cached in the display-info store.
    private static string? ResolveLogin(ActionExecutionContext context, string userId, string? fallbackLogin)
    {
        if (context.StreamEvent.User is { } eventUser
            && eventUser.UserId == userId
            && !string.IsNullOrWhiteSpace(eventUser.Login))
        {
            return eventUser.Login;
        }

        return fallbackLogin;
    }

    private static DateTimeOffset ResolveWindowStartUtc(DateTimeOffset occurredAtUtc, string? resetTimeLocal)
    {
        var timeOfDay = TryParseResetTimeLocal(resetTimeLocal, out var parsed)
            ? parsed
            : new TimeSpan(5, 0, 0);

        var localNow = TimeZoneInfo.ConvertTime(occurredAtUtc, TimeZoneInfo.Local);
        var localDate = localNow.Date;
        var resetBoundary = localDate + timeOfDay;
        if (localNow < resetBoundary)
        {
            resetBoundary = resetBoundary.AddDays(-1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(resetBoundary, TimeZoneInfo.Local);
    }

    private static bool TryParseResetTimeLocal(string? raw, out TimeSpan value)
    {
        if (TimeSpan.TryParse(raw, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
