using FluentAssertions;
using Vulperonex.Application.Counters;
using Vulperonex.Application.Data;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;
using Vulperonex.Application.Twitch;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Chat;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Actions;

public sealed class ExecutorExpansionTests
{
    [Fact]
    public async Task Given_DelayAction_When_Cancelled_Then_OperationIsCancelled()
    {
        var executor = new DelayActionExecutor();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var act = async () => await executor.ExecuteAsync(
            new DelayAction { DelayMs = 30_000 },
            NewContext(),
            cancellationTokenSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_DelayActionWithNegativeDelay_When_Executed_Then_CompletesWithoutThrowing()
    {
        var executor = new DelayActionExecutor();

        var result = await executor.ExecuteAsync(
            new DelayAction { DelayMs = -4 },
            NewContext(),
            TestContext.Current.CancellationToken);

        result.Should().Be(ActionExecutionResult.Completed);
    }

    [Fact]
    public async Task Given_RandomPickerWithSingleChoice_When_Executed_Then_OutputContainsPicked()
    {
        var executor = new RandomPickerActionExecutor();

        var result = await executor.ExecuteAsync(
            new RandomPickerAction { Choices = ["alpha"] },
            NewContext(),
            TestContext.Current.CancellationToken);

        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Picked"].Should().Be("alpha");
    }

    [Fact]
    public async Task Given_ParseChatCommandAction_When_MessageHasMentionTarget_Then_OutputContainsCommandArguments()
    {
        var executor = new ParseChatCommandActionExecutor(new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ParseChatCommandAction { CommandPrefix = "!shoutout" },
            NewContext(messageText: "!shoutout @Alice_Prime extra"),
            TestContext.Current.CancellationToken);

        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["CommandName"].Should().Be("shoutout");
        result.OutputValues!["ArgsText"].Should().Be("@Alice_Prime extra");
        result.OutputValues!["Arg1"].Should().Be("@Alice_Prime");
        result.OutputValues!["Arg2"].Should().Be("extra");
        result.OutputValues!["Target"].Should().Be("Alice_Prime");
        result.OutputValues!["TargetLogin"].Should().Be("Alice_Prime");
        result.OutputValues!["Mention"].Should().Be("@Alice_Prime");
        result.OutputValues!["HasTarget"].Should().Be(true);
    }

    [Fact]
    public async Task Given_ParseChatCommandAction_When_CommandHasNoTarget_Then_HasTargetIsFalse()
    {
        var executor = new ParseChatCommandActionExecutor(new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ParseChatCommandAction { CommandPrefix = "!shoutout" },
            NewContext(messageText: "!shoutout"),
            TestContext.Current.CancellationToken);

        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["CommandName"].Should().Be("shoutout");
        result.OutputValues!["ArgsText"].Should().Be(string.Empty);
        result.OutputValues!["Target"].Should().Be(string.Empty);
        result.OutputValues!["HasTarget"].Should().Be(false);
    }

    [Fact]
    public async Task Given_ParseChatCommandAction_When_MessageTemplateIsConfigured_Then_TemplateIsResolved()
    {
        var executor = new ParseChatCommandActionExecutor(new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ParseChatCommandAction { Message = "!so @{Member.DisplayName}", CommandPrefix = "!so" },
            NewContext(),
            TestContext.Current.CancellationToken);

        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["CommandName"].Should().Be("so");
        result.OutputValues!["Target"].Should().Be("Alice");
    }

    [Fact]
    public async Task Given_UpdateCounterAction_When_Executed_Then_OutputContainsNewValue()
    {
        var repository = new RecordingCounterRepository();
        var executor = new UpdateCounterActionExecutor(repository, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new UpdateCounterAction
            {
                Key = "lottery.tickets.{Member.UserId}",
                Delta = 3,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("lottery.tickets.alice", 3));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Key"].Should().Be("lottery.tickets.alice");
        result.OutputValues!["Value"].Should().Be(3);
    }

    [Fact]
    public async Task Given_TriggerCheckInAction_When_Executed_Then_OutputContainsCheckInCount()
    {
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var settings = new FakeSystemSettingsService();
        var modules = new AlwaysEnabledModuleStateService();
        var cache = new FakePlatformUserDisplayInfoProvider();
        var queryService = new FakeMemberQueryService();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        var transactionProvider = new FakeTransactionProvider();
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver(), bus, modules, settings, cache, queryService, auditLogRepository, transactionProvider);

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().ContainSingle().Which.Should().Be(PlatformIdentity.Create("twitch", "alice"));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Platform"].Should().Be("twitch");
        result.OutputValues!["UserId"].Should().Be("alice");
        result.OutputValues!["DisplayName"].Should().Be("Alice");
        result.OutputValues!["CheckInCount"].Should().Be(1);
        result.OutputValues!["TotalLoyalty"].Should().Be(7);
        result.OutputValues!["RoundIndex"].Should().Be(1);
        result.OutputValues!["StampSlotInRound"].Should().Be(1);

        var checkInEvent = bus.Published.Should().ContainSingle().Subject.Should().BeOfType<MemberCheckedInEvent>().Subject;
        checkInEvent.Platform.Should().Be("twitch");
        checkInEvent.User.UserId.Should().Be("alice");
        checkInEvent.CheckInCount.Should().Be(1);
        checkInEvent.TotalLoyalty.Should().Be(7);
        checkInEvent.RoundIndex.Should().Be(1);
        checkInEvent.StampSlotInRound.Should().Be(1);
        auditLogRepository.Logs.Should().ContainSingle();
        auditLogRepository.Logs[0].ActorKind.Should().Be("workflow");
        auditLogRepository.Logs[0].ActorId.Should().Be("rule-1");
    }

    [Fact]
    public async Task Given_MemberNotBound_When_TriggerCheckInRuns_Then_InvalidOperationIsThrownAndNothingPersists()
    {
        // Incident class 2026-05-27: a chatter without a member binding makes the
        // executor throw, the workflow fails silently, and no overlay card appears.
        // Lock the failure shape so a future auto-bind change is a conscious one.
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        var executor = new TriggerCheckInActionExecutor(
            repository,
            new TemplateResolver(),
            bus,
            new AlwaysEnabledModuleStateService(),
            new FakeSystemSettingsService(),
            new FakePlatformUserDisplayInfoProvider(),
            new NullMemberQueryService(),
            auditLogRepository,
            new FakeTransactionProvider());

        var act = () => executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found before check-in*");
        repository.CheckIns.Should().BeEmpty("a missing member must never be incremented");
        bus.Published.Should().BeEmpty("no overlay card event may be emitted for an unbound chatter");
        auditLogRepository.Logs.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_CheckInAlreadyExistsInCurrentWindow_When_Executed_Then_ResultIsRepeatAndOverlayEventIsPublished()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var settings = new FakeSystemSettingsService();
        var modules = new AlwaysEnabledModuleStateService();
        var cache = new FakePlatformUserDisplayInfoProvider();
        var queryService = new FakeMemberQueryService();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        auditLogRepository.Logs.Add(new MemberAuditLog
        {
            MemberId = "member-1",
            SubjectKind = "member",
            Operation = "checkin",
            OccurredAt = occurredAt,
        });
        var transactionProvider = new FakeTransactionProvider();
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver(), bus, modules, settings, cache, queryService, auditLogRepository, transactionProvider);

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(occurredAt),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().BeEmpty();
        var checkInEvent = bus.Published.Should().ContainSingle().Subject.Should().BeOfType<MemberCheckedInEvent>().Subject;
        checkInEvent.Platform.Should().Be("twitch");
        checkInEvent.User.UserId.Should().Be("alice");
        checkInEvent.User.DisplayName.Should().Be("Alice");
        checkInEvent.CheckInCount.Should().Be(1);
        checkInEvent.TotalLoyalty.Should().Be(7);
        checkInEvent.RoundIndex.Should().Be(1);
        checkInEvent.StampSlotInRound.Should().Be(1);
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Status"].Should().Be("repeat");
        result.OutputValues!["CheckInCount"].Should().Be(1);
        result.OutputValues!["TotalLoyalty"].Should().Be(7);
    }

    [Fact]
    public async Task Given_CheckInOnlyExistsBeforeCurrentWindow_When_Executed_Then_NewCheckInStillSucceeds()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var settings = new FakeSystemSettingsService();
        var modules = new AlwaysEnabledModuleStateService();
        var cache = new FakePlatformUserDisplayInfoProvider();
        var queryService = new FakeMemberQueryService();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        auditLogRepository.Logs.Add(new MemberAuditLog
        {
            MemberId = "member-1",
            SubjectKind = "member",
            Operation = "checkin",
            OccurredAt = occurredAt.AddDays(-2),
        });
        var transactionProvider = new FakeTransactionProvider();
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver(), bus, modules, settings, cache, queryService, auditLogRepository, transactionProvider);

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(occurredAt),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().ContainSingle().Which.Should().Be(PlatformIdentity.Create("twitch", "alice"));
        bus.Published.Should().ContainSingle().Which.Should().BeOfType<MemberCheckedInEvent>();
        result.OutputValues.Should().NotBeNull();
        result.OutputValues.Should().NotContainKey("Status");
        auditLogRepository.Logs.Should().HaveCount(2);
        auditLogRepository.Logs[^1].OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public async Task Given_RepeatCardSettingDisabled_When_CheckInRepeats_Then_CheckInCardIsNotPublished()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var settings = new FakeSystemSettingsService
        {
            RepeatCardEnabled = false,
        };
        var modules = new AlwaysEnabledModuleStateService();
        var cache = new FakePlatformUserDisplayInfoProvider();
        var queryService = new FakeMemberQueryService();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        auditLogRepository.Logs.Add(new MemberAuditLog
        {
            MemberId = "member-1",
            SubjectKind = "member",
            Operation = "checkin",
            OccurredAt = occurredAt,
        });
        var transactionProvider = new FakeTransactionProvider();
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver(), bus, modules, settings, cache, queryService, auditLogRepository, transactionProvider);

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(occurredAt),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().BeEmpty();
        bus.Published.Should().BeEmpty();
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Status"].Should().Be("repeat");
    }

    [Fact]
    public async Task Given_AddLotteryTicketsAction_When_Executed_Then_CounterUsesLotteryTicketKey()
    {
        var repository = new RecordingCounterRepository();
        var executor = new AddLotteryTicketsActionExecutor(repository, new TemplateResolver(), new AlwaysEnabledModuleStateService());

        var result = await executor.ExecuteAsync(
            new AddLotteryTicketsAction
            {
                UserId = "{Member.UserId}",
                Amount = 5,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("lottery.tickets.alice", 5));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Key"].Should().Be("lottery.tickets.alice");
        result.OutputValues!["UserId"].Should().Be("alice");
        result.OutputValues!["TicketsAdded"].Should().Be(5);
        result.OutputValues!["TicketCount"].Should().Be(5);
    }

    [Fact]
    public async Task Given_CheckInModuleDisabled_When_TriggerCheckInRuns_Then_DependencyMissingIsThrown()
    {
        var executor = new TriggerCheckInActionExecutor(
            new RecordingMemberStreamStateRepository(),
            new TemplateResolver(),
            new RecordingStreamEventBus(),
            new DisabledModuleStateService("checkin"),
            new FakeSystemSettingsService(),
            new FakePlatformUserDisplayInfoProvider(),
            new FakeMemberQueryService(),
            new RecordingMemberAuditLogRepository(),
            new FakeTransactionProvider());

        var act = () => executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DependencyMissingException>();
    }

    [Fact]
    public async Task Given_LotteryModuleDisabled_When_AddLotteryTicketsRuns_Then_DependencyMissingIsThrown()
    {
        var executor = new AddLotteryTicketsActionExecutor(
            new RecordingCounterRepository(),
            new TemplateResolver(),
            new DisabledModuleStateService("lottery"));

        var act = () => executor.ExecuteAsync(
            new AddLotteryTicketsAction
            {
                UserId = "{Member.UserId}",
                Amount = 5,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DependencyMissingException>();
    }

    [Fact]
    public async Task Given_EmitSystemEventAction_When_Executed_Then_PublishesTypedSystemEvent()
    {
        var bus = new RecordingStreamEventBus();
        var executor = new EmitSystemEventActionExecutor(bus, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new EmitSystemEventAction
            {
                EventTypeKey = "workflow.followup",
                Payload = new Dictionary<string, string>
                {
                    ["target"] = "{Member.UserId}",
                },
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        var emitted = bus.Published.Should().ContainSingle().Subject.Should().BeOfType<WorkflowSystemEvent>().Subject;
        emitted.EventTypeKey.Should().Be("workflow.followup");
        emitted.Platform.Should().Be("twitch");
        emitted.User!.UserId.Should().Be("alice");
        emitted.Depth.Should().Be(1);
        emitted.Payload["target"].Should().Be("alice");
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["EventTypeKey"].Should().Be("workflow.followup");
        result.OutputValues!["Depth"].Should().Be(1);
    }

    [Fact]
    public async Task Given_TriggerEffectAction_When_Executed_Then_EmitsStrongTypedEffectPayload()
    {
        var emitter = new RecordingOverlayEffectEmitter();
        var executor = new TriggerEffectActionExecutor(emitter, new TemplateResolver(), new FakeClock());

        var result = await executor.ExecuteAsync(
            new TriggerEffectAction
            {
                EffectId = "sparkle-{Member.UserId}",
                DurationMs = 1_500,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        var payload = emitter.Payloads.Should().ContainSingle().Subject;
        payload.SchemaVersion.Should().Be(1);
        payload.EventId.Should().Be("event-1");
        payload.Timestamp.Should().Be(new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero));
        payload.EffectId.Should().Be("sparkle-alice");
        payload.DurationMs.Should().Be(1_500);
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["EffectId"].Should().Be("sparkle-alice");
        result.OutputValues!["DurationMs"].Should().Be(1_500);
    }

    [Fact]
    public async Task Given_EmitOverlayWidgetAction_When_Executed_Then_EmitsStrongTypedWidgetPayload()
    {
        var emitter = new RecordingOverlayWidgetEmitter();
        var executor = new EmitOverlayWidgetActionExecutor(emitter, new TemplateResolver(), new FakeClock());

        var result = await executor.ExecuteAsync(
            new EmitOverlayWidgetAction
            {
                WidgetType = "channel_point",
                OverlayTarget = "alerts",
                DisplayText = "{Member.DisplayName} redeemed",
                Severity = "success",
                DurationMs = 5_000,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        var payload = emitter.Payloads.Should().ContainSingle().Subject;
        payload.SchemaVersion.Should().Be(1);
        payload.EventId.Should().Be("event-1");
        payload.Timestamp.Should().Be(new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero));
        payload.WidgetType.Should().Be("channel_point");
        payload.OverlayTarget.Should().Be("alerts");
        payload.DisplayText.Should().Be("Alice redeemed");
        payload.Severity.Should().Be("success");
        payload.DurationMs.Should().Be(5_000);
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["DisplayText"].Should().Be("Alice redeemed");
    }

    [Fact]
    public async Task Given_LookupTwitchUserAction_When_UserFound_Then_OutputContainsHelixUser()
    {
        var client = new RecordingTwitchHelixClient
        {
            User = new PlatformUserProfile(
                "user-1",
                "alice",
                "Alice Prime",
                "avatar",
                "description",
                IsAffiliate: true),
        };
        var executor = new LookupPlatformUserActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new LookupPlatformUserAction { Login = "{Member.DisplayName}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        client.Requests.Should().ContainSingle().Which.Should().Be(("Alice", null));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["UserId"].Should().Be("user-1");
        result.OutputValues!["Login"].Should().Be("alice");
        result.OutputValues!["DisplayName"].Should().Be("Alice Prime");
        result.OutputValues!["Avatar"].Should().Be("avatar");
        result.OutputValues!["Description"].Should().Be("description");
        result.OutputValues!["IsAffiliate"].Should().Be(true);
        result.OutputValues!["IsFound"].Should().Be(true);
    }

    [Fact]
    public async Task Given_LookupTwitchUserAction_When_ResolverAvailable_Then_TargetIsResolved()
    {
        var client = new RecordingTwitchHelixClient();
        var resolver = new RecordingPlatformUserResolver
        {
            Result = new ResolvedPlatformUser("alice_login", "Alice Prime", "user-1", IsFound: true),
        };
        var executor = new LookupPlatformUserActionExecutor(client, new TemplateResolver(), resolver);

        var result = await executor.ExecuteAsync(
            new LookupPlatformUserAction { Target = "@{Member.DisplayName}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        resolver.Requests.Should().ContainSingle().Which.Should().Be(("twitch", "@Alice"));
        client.Requests.Should().BeEmpty();
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["UserId"].Should().Be("user-1");
        result.OutputValues!["Login"].Should().Be("alice_login");
        result.OutputValues!["DisplayName"].Should().Be("Alice Prime");
        result.OutputValues!["IsFound"].Should().Be(true);
    }

    [Fact]
    public async Task Given_ShoutoutAction_When_Executed_Then_TargetLoginIsResolvedAndOutputContainsResult()
    {
        var client = new RecordingTwitchHelixClient
        {
            ShoutoutResult = new PlatformShoutoutResult(true, "alice", "user-1", "Alice"),
        };
        var executor = new ShoutoutActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ShoutoutAction { TargetLogin = "@{Member.DisplayName}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        client.Shoutouts.Should().ContainSingle().Which.Should().Be("Alice");
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["IsSent"].Should().Be(true);
        result.OutputValues!["TargetLogin"].Should().Be("alice");
        result.OutputValues!["TargetUserId"].Should().Be("user-1");
        result.OutputValues!["TargetDisplayName"].Should().Be("Alice");
    }

    [Fact]
    public async Task Given_ShoutoutAction_When_ResolverAvailable_Then_SendsResolvedLogin()
    {
        var client = new RecordingTwitchHelixClient
        {
            ShoutoutResult = new PlatformShoutoutResult(true, "alice_login", "user-1", "Alice Prime"),
        };
        var resolver = new RecordingPlatformUserResolver
        {
            Result = new ResolvedPlatformUser("alice_login", "Alice Prime", "user-1", IsFound: true),
        };
        var executor = new ShoutoutActionExecutor(client, new TemplateResolver(), resolver);

        var result = await executor.ExecuteAsync(
            new ShoutoutAction { TargetLogin = "@{Member.DisplayName}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        resolver.Requests.Should().ContainSingle().Which.Should().Be(("twitch", "@Alice"));
        client.Shoutouts.Should().ContainSingle().Which.Should().Be("alice_login");
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["IsSent"].Should().Be(true);
        result.OutputValues!["TargetLogin"].Should().Be("alice_login");
        result.OutputValues!["TargetUserId"].Should().Be("user-1");
        result.OutputValues!["TargetDisplayName"].Should().Be("Alice Prime");
    }

    [Fact]
    public async Task Given_ShoutoutAction_When_HelixThrows_Then_ReturnsNotSentAndDoesNotAbortRule()
    {
        // Best-effort shoutout: a Helix failure (e.g. Twitch not authorized) must be
        // swallowed into IsSent=false so the rule continues to its next step instead of
        // aborting before a follow-up chat message can be sent.
        var client = new RecordingTwitchHelixClient
        {
            ShoutoutException = new InvalidOperationException("Twitch OAuth authorization is required."),
        };
        var executor = new ShoutoutActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ShoutoutAction { TargetLogin = "@{Member.DisplayName}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        client.Shoutouts.Should().ContainSingle();
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["IsSent"].Should().Be(false);
    }

    [Fact]
    public async Task Given_ShoutoutAction_When_EventIsSimulated_Then_SkipsHelixAndReturnsSyntheticSuccess()
    {
        // Simulated events must not hit real Twitch. The executor returns synthetic IsSent=true
        // and never calls the Helix client, so downstream steps still run under simulation.
        var client = new RecordingTwitchHelixClient
        {
            ShoutoutResult = new PlatformShoutoutResult(true, "alice", "user-1", "Alice"),
        };
        var executor = new ShoutoutActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new ShoutoutAction { TargetLogin = "@{Member.DisplayName}" },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        client.Shoutouts.Should().BeEmpty();
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["IsSent"].Should().Be(true);
        result.OutputValues!["TargetLogin"].Should().Be("Alice");
        result.OutputValues!["TargetUserId"].Should().Be(string.Empty);
        result.OutputValues!["TargetDisplayName"].Should().Be("Alice");
    }

    [Fact]
    public async Task Given_RefundAction_When_EventIsSimulated_Then_SkipsHelixAndReturnsSyntheticRefund()
    {
        // §4.27: a simulated reward.redeemed must not issue a real Twitch refund.
        var client = new RecordingTwitchHelixClient { RefundResult = true };
        var executor = new RefundRewardRedemptionActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new RefundRewardRedemptionAction { RewardId = "reward-x", RedemptionId = "redemption-x" },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        client.Refunds.Should().BeEmpty();
        result.OutputValues!["IsRefunded"].Should().Be(true);
    }

    [Fact]
    public async Task Given_LookupAction_When_EventIsSimulated_Then_SkipsHelixAndReturnsSyntheticUser()
    {
        // §4.27: a simulated event must not hit the real Twitch users API.
        var client = new RecordingTwitchHelixClient();
        var executor = new LookupPlatformUserActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new LookupPlatformUserAction { Login = "someuser" },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        client.Requests.Should().BeEmpty();
        result.OutputValues!["IsFound"].Should().Be(true);
        result.OutputValues!["Login"].Should().Be("someuser");
    }

    [Fact]
    public async Task Given_UpdateCounter_When_SimulatedAndWritesSuppressed_Then_SkipsRealIncrement()
    {
        // §4.27: persistent writes suppressed by default under simulation (setting defaults false).
        var repository = new RecordingCounterRepository();
        var executor = new UpdateCounterActionExecutor(repository, new TemplateResolver(), new FakeSystemSettingsService());

        var result = await executor.ExecuteAsync(
            new UpdateCounterAction { Key = "wins", Delta = 3 },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().BeEmpty();
        result.OutputValues!["Value"].Should().Be(3);
    }

    [Fact]
    public async Task Given_UpdateCounter_When_SimulatedAndWritesAllowed_Then_IncrementsCounter()
    {
        var repository = new RecordingCounterRepository();
        var settings = new FakeSystemSettingsService { SimulationAllowPersistentWrites = true };
        var executor = new UpdateCounterActionExecutor(repository, new TemplateResolver(), settings);

        var result = await executor.ExecuteAsync(
            new UpdateCounterAction { Key = "wins", Delta = 3 },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("wins", 3));
        result.OutputValues!["Value"].Should().Be(3);
    }

    [Fact]
    public async Task Given_AddLotteryTickets_When_SimulatedAndWritesSuppressed_Then_SkipsRealIncrement()
    {
        var repository = new RecordingCounterRepository();
        var executor = new AddLotteryTicketsActionExecutor(
            repository,
            new TemplateResolver(),
            new AlwaysEnabledModuleStateService(),
            new FakeSystemSettingsService());

        var result = await executor.ExecuteAsync(
            new AddLotteryTicketsAction { UserId = "{Member.UserId}", Amount = 5 },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().BeEmpty();
        result.OutputValues!["TicketCount"].Should().Be(5);
    }

    [Fact]
    public async Task Given_AddLotteryTickets_When_SimulatedAndWritesAllowed_Then_IncrementsTickets()
    {
        var repository = new RecordingCounterRepository();
        var settings = new FakeSystemSettingsService { SimulationAllowPersistentWrites = true };
        var executor = new AddLotteryTicketsActionExecutor(
            repository,
            new TemplateResolver(),
            new AlwaysEnabledModuleStateService(),
            settings);

        var result = await executor.ExecuteAsync(
            new AddLotteryTicketsAction { UserId = "{Member.UserId}", Amount = 5 },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("lottery.tickets.alice", 5));
        result.OutputValues!["TicketCount"].Should().Be(5);
    }

    [Fact]
    public async Task Given_TriggerCheckIn_When_SimulatedAndWritesSuppressed_Then_SkipsDbWriteButEmitsCard()
    {
        // §4.27: no DB increment / audit, but still emit the overlay card so the preview reacts.
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        var executor = new TriggerCheckInActionExecutor(
            repository,
            new TemplateResolver(),
            bus,
            new AlwaysEnabledModuleStateService(),
            new FakeSystemSettingsService(),
            new FakePlatformUserDisplayInfoProvider(),
            new FakeMemberQueryService(),
            auditLogRepository,
            new FakeTransactionProvider());

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().BeEmpty();
        auditLogRepository.Logs.Should().BeEmpty();
        bus.Published.Should().ContainSingle().Which.Should().BeOfType<MemberCheckedInEvent>();
        result.OutputValues!.Should().ContainKey("CheckInCount");
    }

    [Fact]
    public async Task Given_TriggerCheckIn_When_SimulatedAndWritesAllowed_Then_PersistsCheckIn()
    {
        var repository = new RecordingMemberStreamStateRepository();
        var bus = new RecordingStreamEventBus();
        var auditLogRepository = new RecordingMemberAuditLogRepository();
        var settings = new FakeSystemSettingsService { SimulationAllowPersistentWrites = true };
        var executor = new TriggerCheckInActionExecutor(
            repository,
            new TemplateResolver(),
            bus,
            new AlwaysEnabledModuleStateService(),
            settings,
            new FakePlatformUserDisplayInfoProvider(),
            new FakeMemberQueryService(),
            auditLogRepository,
            new FakeTransactionProvider());

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(platform: "simulation"),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().ContainSingle().Which.Should().Be(PlatformIdentity.Create("simulation", "alice"));
        auditLogRepository.Logs.Should().ContainSingle();
        bus.Published.Should().ContainSingle().Which.Should().BeOfType<MemberCheckedInEvent>();
        result.OutputValues!["CheckInCount"].Should().Be(1);
    }

    [Fact]
    public async Task Given_RefundTwitchRedemptionAction_When_Executed_Then_RewardAndRedemptionAreResolved()
    {
        var client = new RecordingTwitchHelixClient { RefundResult = true };
        var executor = new RefundRewardRedemptionActionExecutor(client, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new RefundRewardRedemptionAction
            {
                RewardId = "{Trigger.RewardId}",
                RedemptionId = "{Trigger.RedemptionId}",
            },
            NewRewardContext(),
            TestContext.Current.CancellationToken);

        client.Refunds.Should().ContainSingle().Which.Should().Be(("reward-1", "redemption-1"));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["IsRefunded"].Should().Be(true);
        result.OutputValues!["RewardId"].Should().Be("reward-1");
        result.OutputValues!["RedemptionId"].Should().Be("redemption-1");
    }

    private static ActionExecutionContext NewContext(
        DateTimeOffset? occurredAt = null,
        string platform = "twitch",
        string messageText = "")
    {
        var streamEvent = new UserSentMessageEvent
        {
            EventId = "event-1",
            Platform = platform,
            User = new StreamUser(platform, "alice", "Alice"),
            MessageText = messageText,
            OccurredAt = occurredAt ?? new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero),
        };

        return new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "rule-1", Name = "Rule", EventTypeKey = StreamEventKeys.UserSentMessage },
            ActionIndex: 0,
            ExpressionContext: new Vulperonex.Application.Expressions.ExpressionContext(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UserId"] = "alice",
                    ["DisplayName"] = "Alice",
                }));
    }

    private static ActionExecutionContext NewRewardContext()
    {
        var streamEvent = new RewardRedeemedEvent
        {
            EventId = "event-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            RewardId = "reward-1",
            RewardTitle = "Highlight",
            RedemptionId = "redemption-1",
        };

        return new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "rule-1", Name = "Rule", EventTypeKey = StreamEventKeys.RewardRedeemed },
            ActionIndex: 0,
            ExpressionContext: new Vulperonex.Application.Expressions.ExpressionContext(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RewardId"] = "reward-1",
                    ["RewardTitle"] = "Highlight",
                    ["RedemptionId"] = "redemption-1",
                },
                new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UserId"] = "alice",
                    ["DisplayName"] = "Alice",
                }));
    }

    private sealed class RecordingCounterRepository : ICounterRepository
    {
        public List<(string Key, long Delta)> Calls { get; } = [];

        public Task<long> IncrementAsync(string key, long delta, CancellationToken cancellationToken = default)
        {
            Calls.Add((key, delta));
            return Task.FromResult(delta);
        }
    }

    private sealed class RecordingMemberStreamStateRepository : IMemberStreamStateRepository
    {
        public List<PlatformIdentity> CheckIns { get; } = [];

        public Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> IncrementCheckInAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            CheckIns.Add(identity);
            return Task.FromResult(CheckIns.Count);
        }
    }

    private sealed class RecordingStreamEventBus : IStreamEventBus
    {
        public List<IStreamEvent> Published { get; } = [];

        public IObservable<IStreamEvent> Events { get; } = new System.Reactive.Subjects.Subject<IStreamEvent>();

        public Task PublishAsync(IStreamEvent streamEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(streamEvent);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : IStreamEvent
        {
            return new NoOpDisposable();
        }

        public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingOverlayEffectEmitter : IOverlayEffectEmitter
    {
        public List<OverlayEffectPayload> Payloads { get; } = [];

        public Task EmitAsync(OverlayEffectPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOverlayWidgetEmitter : IOverlayWidgetEmitter
    {
        public List<OverlayWidgetPayload> Payloads { get; } = [];

        public Task EmitAsync(OverlayWidgetPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTwitchHelixClient : IHelixClient
    {
        public PlatformUserProfile? User { get; init; }
        public PlatformShoutoutResult? ShoutoutResult { get; init; }
        public Exception? ShoutoutException { get; init; }
        public bool RefundResult { get; init; }
        public List<(string? Login, string? UserId)> Requests { get; } = [];
        public List<string> Shoutouts { get; } = [];
        public List<(string RewardId, string RedemptionId)> Refunds { get; } = [];

        public Task<PlatformUserProfile?> LookupUserAsync(
            string? login,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((login, userId));
            return Task.FromResult(User);
        }

        public Task<PlatformUserProfile?> SearchChannelExactAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlatformUserProfile?>(null);
        }

        public Task<PlatformShoutoutResult> SendShoutoutAsync(
            string targetLogin,
            CancellationToken cancellationToken = default)
        {
            Shoutouts.Add(targetLogin);
            if (ShoutoutException is not null)
            {
                return Task.FromException<PlatformShoutoutResult>(ShoutoutException);
            }
            return Task.FromResult(ShoutoutResult ?? new PlatformShoutoutResult(false, targetLogin, null, null));
        }

        public Task<bool> RefundRedemptionAsync(
            string rewardId,
            string redemptionId,
            CancellationToken cancellationToken = default)
        {
            Refunds.Add((rewardId, redemptionId));
            return Task.FromResult(RefundResult);
        }

        public Task<IReadOnlyList<PlatformBadgeDescriptor>> GetGlobalBadgesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformBadgeDescriptor>>([]);
        }

        public Task<IReadOnlyList<PlatformBadgeDescriptor>> GetChannelBadgesAsync(
            string broadcasterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformBadgeDescriptor>>([]);
        }

        public Task CreateEventSubSubscriptionAsync(
            string type,
            string version,
            IReadOnlyDictionary<string, string> condition,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlatformRewardDescriptor>> GetCustomRewardsAsync(
            string broadcasterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformRewardDescriptor>>([]);
        }
    }

    private sealed class RecordingPlatformUserResolver : IPlatformUserResolver
    {
        public ResolvedPlatformUser Result { get; init; } = new(string.Empty, string.Empty, null, IsFound: false);
        public List<(string Platform, string Input)> Requests { get; } = [];

        public Task<ResolvedPlatformUser> ResolveAsync(
            string platform,
            string input,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((platform, input));
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeSystemSettingsService : Vulperonex.Application.Settings.ISystemSettingsService
    {
        public bool RepeatCardEnabled { get; init; } = true;
        public bool SimulationAllowPersistentWrites { get; init; }

        public IObservable<Vulperonex.Application.Settings.SettingChangedEvent> Changes => 
            new System.Reactive.Subjects.Subject<Vulperonex.Application.Settings.SettingChangedEvent>();

        public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            if (key == "overlay.member.stamps_per_round" && typeof(T) == typeof(int))
            {
                return Task.FromResult((T)(object)10);
            }
            if (key == "checkin.reset_time_local" && typeof(T) == typeof(string))
            {
                return Task.FromResult((T)(object)"05:00");
            }
            if (key == "checkin.repeat_card_enabled" && typeof(T) == typeof(bool))
            {
                return Task.FromResult((T)(object)RepeatCardEnabled);
            }
            if (key == Vulperonex.Application.Settings.SystemSettingKey.SimulationAllowPersistentWrites && typeof(T) == typeof(bool))
            {
                return Task.FromResult((T)(object)SimulationAllowPersistentWrites);
            }
            return Task.FromResult(defaultValue);
        }

        public Task SetAsync<T>(string key, T value, string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePlatformUserDisplayInfoProvider : IPlatformUserDisplayInfoProvider
    {
        public Task<PlatformUserDisplayInfo?> GetAsync(
            string platform, 
            string platformUserId, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlatformUserDisplayInfo?>(null);
        }
    }

    private sealed class RecordingWorkflowChatOverlaySink : IWorkflowChatOverlaySink
    {
        public List<string> AssistantMessages { get; } = [];
        public List<WorkflowCheckInCardOverlayMessage> CheckInCards { get; } = [];

        public Task PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            AssistantMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishCheckInCardAsync(
            WorkflowCheckInCardOverlayMessage message,
            CancellationToken cancellationToken = default)
        {
            CheckInCards.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class NullMemberQueryService : IMemberQueryService
    {
        public Task<IReadOnlyList<MemberReadModel>> ListAsync(string? platform = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemberReadModel>>([]);

        public Task<MemberReadModel?> FindByMemberIdAsync(string memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<MemberReadModel?>(null);

        public Task<MemberReadModel?> FindByIdentityAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
            => Task.FromResult<MemberReadModel?>(null);
    }

    private sealed class FakeMemberQueryService : IMemberQueryService
    {
        public Task<IReadOnlyList<MemberReadModel>> ListAsync(string? platform = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemberReadModel>>([]);

        public Task<MemberReadModel?> FindByMemberIdAsync(string memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<MemberReadModel?>(null);

        public Task<MemberReadModel?> FindByIdentityAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MemberReadModel?>(new MemberReadModel(
                "member-1",
                [new PlatformIdentityReadModel(identity.Platform, identity.PlatformUserId)],
                new LoyaltyReadModel(7, 1),
                123L));
        }
    }

    private sealed class RecordingMemberAuditLogRepository : IMemberAuditLogRepository
    {
        public List<MemberAuditLog> Logs { get; } = [];

        public Task AppendAsync(MemberAuditLog log, CancellationToken cancellationToken)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MemberAuditLog>> QueryAsync(string memberId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemberAuditLog>>(Logs
                .Where(log => log.MemberId == memberId)
                .OrderByDescending(log => log.OccurredAt)
                .Skip(offset)
                .Take(limit)
                .ToList());
    }

    private sealed class AlwaysEnabledModuleStateService : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ModuleToggleResult> ToggleAsync(string moduleName, bool enabled, string actorKind, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DisabledModuleStateService(string disabledName) : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(!string.Equals(moduleName, disabledName, StringComparison.OrdinalIgnoreCase));

        public Task<ModuleToggleResult> ToggleAsync(string moduleName, bool enabled, string actorKind, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeTransactionProvider : ITransactionProvider
    {
        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ITransactionScope>(new FakeTransactionScope());
        }

        private sealed class FakeTransactionScope : ITransactionScope
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
