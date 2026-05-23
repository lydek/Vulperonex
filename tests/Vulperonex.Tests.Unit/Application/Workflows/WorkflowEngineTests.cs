using FluentAssertions;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows;

public sealed class WorkflowEngineTests
{
    [Fact]
    public async Task Given_MatchingRule_When_EventIsPublished_Then_ActionExecutesOnce()
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        var rule = NewRule(actions: [new SendChatMessageAction { Template = "Hello {user.displayName}" }]);
        await using var engine = NewEngine(bus, [rule], [new SendChatMessageActionExecutor([sender], new TemplateRenderer())]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(NewMessageEvent(), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        sender.Messages.Should().ContainSingle().Which.Should().Be("Hello Alice");
    }

    [Fact]
    public async Task Given_SimulationAdapter_When_MessageIsSimulated_Then_WorkflowEngineSendsChatMessage()
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        var rule = NewRule(actions: [new SendChatMessageAction { Template = "Echo {event.message}" }]);
        await using var engine = NewEngine(bus, [rule], [new SendChatMessageActionExecutor([sender], new TemplateRenderer())]);
        var simulation = new SimulationAdapter(bus, new Vulperonex.Infrastructure.EventTypes.InMemoryStreamEventTypeRegistry());
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await simulation.SimulateAsync(
            SimulationRequest.Message(
                "twitch",
                new StreamUser("twitch", "alice", "Alice"),
                "!hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        sender.Messages.Should().ContainSingle().Which.Should().Be("Echo !hello");
    }

    [Fact]
    public async Task Given_DisabledRule_When_EventIsPublished_Then_ActionIsSkipped()
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        var rule = NewRule(
            isEnabled: false,
            actions: [new SendChatMessageAction { Template = "Hello" }]);
        await using var engine = NewEngine(bus, [rule], [new SendChatMessageActionExecutor([sender], new TemplateRenderer())]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(NewMessageEvent(), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        sender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MultipleMatchingRules_When_EventIsPublished_Then_RulesExecuteByPriorityCreatedAtAndId()
    {
        await using var bus = new InMemoryStreamEventBus();
        var recorder = new RecordingActionExecutor();
        var rules = new[]
        {
            NewRule(id: "rule-c", priority: 2, createdAt: new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero)),
            NewRule(id: "rule-b", priority: 1, createdAt: new DateTimeOffset(2026, 5, 14, 12, 0, 1, TimeSpan.Zero)),
            NewRule(id: "rule-a", priority: 1, createdAt: new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero)),
        };
        await using var engine = NewEngine(bus, rules, [recorder]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(NewMessageEvent(), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        recorder.Executions.Select(execution => execution.RuleId)
            .Should().Equal("rule-a", "rule-b", "rule-c");
    }

    [Fact]
    public async Task Given_SameEventRuleAndAction_When_EventIsReplayed_Then_ActionExecutionIsDeduplicated()
    {
        var bus = new InMemoryStreamEventBus();
        await using var disposableBus = bus;
        var recorder = new RecordingActionExecutor();
        var rule = NewRule();
        await using var engine = NewEngine(bus, [rule], [recorder]);
        await engine.StartAsync(TestContext.Current.CancellationToken);
        var streamEvent = NewMessageEvent(eventId: "event-1");

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        recorder.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_ContinueOnError_When_ActionFails_Then_NextActionExecutes()
    {
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.ContinueOnError },
            new TestAction(),
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_StopOnError_When_ActionFails_Then_NextActionIsSkipped()
    {
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.StopOnError },
            new TestAction(),
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_RetryOnError_When_ActionFailsOnce_Then_ActionIsRetried()
    {
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction
            {
                ErrorBehavior = ErrorBehavior.RetryOnError,
                MaxRetries = 1,
                BackoffMs = 0,
            },
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ActionTimeout_When_ActionRunsTooLong_Then_CancellationIsRequested()
    {
        var executor = new CancellationObservingActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction
            {
                TimeoutMs = 1,
                ErrorBehavior = ErrorBehavior.ContinueOnError,
            },
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.SawCancellation.Should().BeTrue();
    }

    [Fact]
    public async Task Given_ExecutionIsCancelled_When_EventIsReplayed_Then_ActionCanRunAgain()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var executor = new CancelFirstExecutionActionExecutor(cancellationTokenSource);
        var rule = NewRule();
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);
        var streamEvent = NewMessageEvent(eventId: "event-cancelled");

        var firstRun = async () => await engine.ExecuteRuleAsync(rule, streamEvent, cancellationTokenSource.Token);
        await firstRun.Should().ThrowAsync<OperationCanceledException>();
        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);

        executor.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task Given_TerminalActionFailure_When_EventIsReplayed_Then_FailedActionIsSkipped()
    {
        await using var bus = new InMemoryStreamEventBus();
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.ContinueOnError },
        ]);
        await using var engine = NewEngine(bus, [rule], [executor]);
        var streamEvent = NewMessageEvent(eventId: "event-1");

        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);
        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);

        // First call records a failure; second call must NOT re-run the action because
        // SPEC §4.2 requires Completed/Failed entries to skip on replay.
        executor.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_RetryExhausted_When_EventIsReplayed_Then_FailedActionIsSkipped()
    {
        await using var bus = new InMemoryStreamEventBus();
        var executor = new AlwaysFailingActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction
            {
                ErrorBehavior = ErrorBehavior.RetryOnError,
                MaxRetries = 2,
                BackoffMs = 0,
            },
        ]);
        await using var engine = NewEngine(bus, [rule], [executor]);
        var streamEvent = NewMessageEvent(eventId: "event-2");

        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);
        var attemptsAfterFirstCall = executor.Attempts;
        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);

        attemptsAfterFirstCall.Should().Be(3); // initial attempt + 2 retries
        executor.Attempts.Should().Be(attemptsAfterFirstCall); // replay skipped after Failed
    }

    [Fact]
    public async Task Given_ActionExecutionConditionFalse_When_ExecutingRule_Then_ActionIsSkippedAndNextActionRuns()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction { ExecutionCondition = "Trigger.MessageText == 'nope'" },
            new TestAction(),
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().ContainSingle().Which.ActionIndex.Should().Be(1);
    }

    [Fact]
    public async Task Given_ActionOutputVariable_When_ExecutingNextAction_Then_OutputIsAvailableInExpressionContext()
    {
        var executor = new RecordingActionExecutor(
            outputsByActionIndex: new Dictionary<int, IReadOnlyDictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?>
                {
                    ["DisplayName"] = "Alice Prime",
                    ["Points"] = 42,
                },
            });
        var rule = NewRule(actions:
        [
            new TestAction { OutputVariable = "Lookup" },
            new TestAction { ExecutionCondition = "Step.Lookup.Points == 42" },
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().HaveCount(2);
        executor.Contexts[1].ExpressionContext.Steps["Lookup"]["DisplayName"].Should().Be("Alice Prime");
    }

    [Fact]
    public async Task Given_GlobalCooldown_When_RuleRunsTwiceImmediately_Then_SecondRunIsSkipped()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with
        {
            Throttle = new WorkflowThrottlePolicy(CooldownSeconds: 30),
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(eventId: "event-1"), TestContext.Current.CancellationToken);
        await engine.ExecuteRuleAsync(rule, NewMessageEvent(eventId: "event-2"), TestContext.Current.CancellationToken);

        executor.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_PerUserCooldown_When_DifferentUserRunsImmediately_Then_SecondRunExecutes()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with
        {
            Throttle = new WorkflowThrottlePolicy(
                PerUserCooldown: true,
                PerUserCooldownSeconds: 30),
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(
            rule,
            NewMessageEvent(eventId: "event-1", userId: "alice", displayName: "Alice"),
            TestContext.Current.CancellationToken);
        await engine.ExecuteRuleAsync(
            rule,
            NewMessageEvent(eventId: "event-2", userId: "bob", displayName: "Bob"),
            TestContext.Current.CancellationToken);
        await engine.ExecuteRuleAsync(
            rule,
            NewMessageEvent(eventId: "event-3", userId: "alice", displayName: "Alice"),
            TestContext.Current.CancellationToken);

        executor.Executions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_RuleTimeoutShorterThanActionTimeout_When_ActionRunsTooLong_Then_ActionIsAbandoned()
    {
        var executor = new CancellationObservingActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction
            {
                TimeoutMs = 10_000,
                ErrorBehavior = ErrorBehavior.ContinueOnError,
            },
        ]) with
        {
            TimeoutSeconds = 1,
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.SawCancellation.Should().BeTrue();
    }

    [Fact]
    public async Task Given_MainActionStopsOnError_When_OnFailureStepsExist_Then_OnFailureRunsWithFailureContext()
    {
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.StopOnError },
            new TestAction(),
        ]) with
        {
            OnFailureSteps =
            [
                new TestAction { ExecutionCondition = "Failure.StepIndex == 0" },
            ],
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().Equal(
            ("rule-1", 0, WorkflowExecutionPhase.Main),
            ("rule-1", 0, WorkflowExecutionPhase.OnFailure));
        executor.Contexts[1].ExpressionContext.Failure["ErrorMessage"].Should().Be("Expected test failure.");
    }

    [Fact]
    public async Task Given_OnFailureStepFails_When_ExecutingRule_Then_NoSecondOnFailureRuns()
    {
        var executor = new FailByPhaseActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.StopOnError },
        ]) with
        {
            OnFailureSteps =
            [
                new TestAction { ErrorBehavior = ErrorBehavior.StopOnError },
            ],
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().Equal(
            WorkflowExecutionPhase.Main,
            WorkflowExecutionPhase.OnFailure);
    }

    [Fact]
    public async Task Given_MainAndOnFailureSameActionIndex_When_EventIsReplayed_Then_PhasesUseSeparateDedupKeys()
    {
        var executor = new RecordingActionExecutor(failFirstExecution: true);
        var rule = NewRule(actions:
        [
            new TestAction { ErrorBehavior = ErrorBehavior.StopOnError },
        ]) with
        {
            OnFailureSteps = [new TestAction()],
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);
        var streamEvent = NewMessageEvent();

        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);
        await engine.ExecuteRuleAsync(rule, streamEvent, TestContext.Current.CancellationToken);

        executor.Executions.Should().Equal(
            ("rule-1", 0, WorkflowExecutionPhase.Main),
            ("rule-1", 0, WorkflowExecutionPhase.OnFailure));
    }

    [Fact]
    public async Task Given_CachedRuleInvalidatedDuringExecution_When_ActionChainContinues_Then_InFlightSnapshotIsUsed()
    {
        await using var bus = new InMemoryStreamEventBus();
        var rule = NewRule(actions: [new TestAction(), new TestAction()]);
        var queryService = new FakeWorkflowRuleQueryService([rule]);
        var cache = new InMemoryRuleSnapshotCache(queryService);
        var executor = new BlockingFirstActionExecutor();
        await using var engine = NewEngine(bus, cache, [executor]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(NewMessageEvent(), TestContext.Current.CancellationToken);
        await executor.FirstActionStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        queryService.Rules = [rule with { Actions = [new TestAction()] }];
        cache.Invalidate(rule.Id);
        executor.ReleaseFirstAction();
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        executor.Executions.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Given_TriggerFilter_When_FilterValueDiffersOnlyByCase_Then_RuleExecutes()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with
        {
            Trigger = new WorkflowTrigger(
                StreamEventKeys.UserSentMessage,
                new Dictionary<string, string> { ["MessageText"] = "!HELLO" }),
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(messageText: "!hello"), TestContext.Current.CancellationToken);

        executor.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_MatchConditionFalse_When_ExecutingRule_Then_RuleIsSkipped()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with
        {
            MatchCondition = "Trigger.MessageText == '!other' && Member.DisplayName == 'Alice'",
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(messageText: "!hello"), TestContext.Current.CancellationToken);

        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SubWorkflowRule_When_EventIsPublished_Then_RuleIsNotTriggeredDirectly()
    {
        await using var bus = new InMemoryStreamEventBus();
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with { IsSubWorkflow = true };
        await using var engine = NewEngine(bus, [rule], [executor]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(NewMessageEvent(), TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_InvokeSubWorkflowArgs_When_ParentOutputExists_Then_ChildReceivesResolvedArgs()
    {
        var lateInvoker = new LateBoundWorkflowRuleInvoker();
        var recorder = new RecordingActionExecutor(
            outputsByActionIndex: new Dictionary<int, IReadOnlyDictionary<string, object?>>
            {
                [0] = new Dictionary<string, object?> { ["DisplayName"] = "Alice Prime" },
            });
        var parent = NewRule(id: "parent", actions:
        [
            new TestAction { OutputVariable = "Lookup" },
            new InvokeSubWorkflowAction
            {
                WorkflowId = "child",
                Args = new Dictionary<string, string>
                {
                    ["Target"] = "{Step.Lookup.DisplayName}",
                },
            },
        ]);
        var child = NewRule(id: "child", actions:
        [
            new TestAction { ExecutionCondition = "Args.Target == 'Alice Prime'" },
        ]) with
        {
            IsSubWorkflow = true,
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(
            bus,
            [parent, child],
            [recorder, new InvokeSubWorkflowActionExecutor(() => lateInvoker, new TemplateResolver())]);
        lateInvoker.Inner = engine;

        await engine.ExecuteRuleAsync(parent, NewMessageEvent(), TestContext.Current.CancellationToken);

        recorder.Contexts.Should().Contain(context =>
            context.WorkflowRule.Id == "child" && context.ExpressionContext.Args["Target"] == "Alice Prime");
    }

    [Fact]
    public async Task Given_StopIfConditionTrue_When_ExecutingRule_Then_PipelineStopsWithoutOnFailure()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule(actions:
        [
            new StopIfAction { Condition = "Trigger.MessageText == '!hello'" },
            new TestAction(),
        ]) with
        {
            OnFailureSteps = [new TestAction()],
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(
            bus,
            [rule],
            [executor, new StopIfActionExecutor(new NCalcExpressionEvaluator())]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_RandomPickerOutputVariable_When_NextActionRuns_Then_PickedIsAvailableInStepContext()
    {
        var recorder = new RecordingActionExecutor();
        var rule = NewRule(actions:
        [
            new RandomPickerAction { Choices = ["alpha"], OutputVariable = "Pick" },
            new TestAction { ExecutionCondition = "Step.Pick.Picked == 'alpha'" },
        ]);
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(
            bus,
            [rule],
            [recorder, new RandomPickerActionExecutor()]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        recorder.Executions.Should().ContainSingle().Which.ActionIndex.Should().Be(1);
    }

    [Fact]
    public async Task Given_SystemEventDepthOverCap_When_Published_Then_WorkflowIsSkipped()
    {
        await using var bus = new InMemoryStreamEventBus();
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with { EventTypeKey = "workflow.followup" };
        await using var engine = NewEngine(bus, [rule], [executor]);
        await engine.StartAsync(TestContext.Current.CancellationToken);

        await bus.PublishAsync(new WorkflowSystemEvent
        {
            EventTypeKey = "workflow.followup",
            Platform = "system",
            Depth = 6,
        }, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SystemEventPayloadFilter_When_PayloadMatches_Then_RuleExecutes()
    {
        var executor = new RecordingActionExecutor();
        var rule = NewRule() with
        {
            EventTypeKey = "workflow.followup",
            Trigger = new WorkflowTrigger(
                "workflow.followup",
                new Dictionary<string, string> { ["Payload.target"] = "alice" }),
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(
            rule,
            new WorkflowSystemEvent
            {
                EventTypeKey = "workflow.followup",
                Platform = "system",
                Depth = 1,
                Payload = new Dictionary<string, string> { ["target"] = "alice" },
            },
            TestContext.Current.CancellationToken);

        executor.Executions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_ParallelRuleWithMaxParallelismAboveCap_When_ExecutingActions_Then_ConcurrencyIsClampedToCap()
    {
        var executor = new ConcurrencyTrackingActionExecutor();
        var rule = NewRule(actions: Enumerable.Range(0, 100).Select(_ => (WorkflowAction)new TestAction()).ToArray()) with
        {
            ExecutionMode = WorkflowExecutionMode.Parallel,
            MaxParallelism = 10_000,
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.MaxObservedConcurrency.Should().BeLessThanOrEqualTo(64);
    }

    [Fact]
    public async Task Given_ParallelRule_When_ExecutingActions_Then_MaxParallelismIsRespected()
    {
        var executor = new ConcurrencyTrackingActionExecutor();
        var rule = NewRule(actions:
        [
            new TestAction(),
            new TestAction(),
            new TestAction(),
        ]) with
        {
            ExecutionMode = WorkflowExecutionMode.Parallel,
            MaxParallelism = 2,
        };
        await using var bus = new InMemoryStreamEventBus();
        await using var engine = NewEngine(bus, [rule], [executor]);

        await engine.ExecuteRuleAsync(rule, NewMessageEvent(), TestContext.Current.CancellationToken);

        executor.MaxObservedConcurrency.Should().Be(2);
    }

    private static WorkflowEngine NewEngine(
        IStreamEventBus bus,
        IReadOnlyCollection<WorkflowRule> rules,
        IEnumerable<IWorkflowActionExecutor> executors)
    {
        return NewEngine(bus, new InMemoryRuleSnapshotCache(new FakeWorkflowRuleQueryService(rules)), executors);
    }

    private static WorkflowEngine NewEngine(
        IStreamEventBus bus,
        IRuleSnapshotCache ruleSnapshotCache,
        IEnumerable<IWorkflowActionExecutor> executors)
    {
        return new WorkflowEngine(
            bus,
            ruleSnapshotCache,
            new WorkflowConditionEvaluator(new FakeClock()),
            executors,
            new InMemoryWorkflowActionExecutionStore(),
            new NCalcExpressionEvaluator(),
            new InMemoryWorkflowThrottleService(new FakeClock()),
            new FakeClock());
    }

    private static WorkflowRule NewRule(
        string id = "rule-1",
        int priority = 0,
        DateTimeOffset? createdAt = null,
        bool isEnabled = true,
        IReadOnlyList<WorkflowAction>? actions = null)
    {
        return new WorkflowRule
        {
            Id = id,
            Name = id,
            EventTypeKey = StreamEventKeys.UserSentMessage,
            IsEnabled = isEnabled,
            Priority = priority,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Actions = actions ?? [new TestAction()],
        };
    }

    private static UserSentMessageEvent NewMessageEvent(
        string eventId = "event-1",
        string userId = "alice",
        string displayName = "Alice",
        string messageText = "!hello")
    {
        return new UserSentMessageEvent
        {
            EventId = eventId,
            Platform = "twitch",
            User = new StreamUser("twitch", userId, displayName),
            MessageText = messageText,
        };
    }

    private sealed class RecordingChatSender(string platform) : IPlatformChatSender
    {
        public string Platform { get; } = platform;
        public List<string> Messages { get; } = [];

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record TestAction : WorkflowAction
    {
        public const string TestActionType = "test";
        public override string Type => TestActionType;
    }

    private sealed class RecordingActionExecutor(
        bool failFirstExecution = false,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object?>>? outputsByActionIndex = null) : IWorkflowActionExecutor
    {
        private bool _hasFailed;
        public string ActionType => TestAction.TestActionType;
        public List<(string RuleId, int ActionIndex, WorkflowExecutionPhase Phase)> Executions { get; } = [];
        public List<ActionExecutionContext> Contexts { get; } = [];

        public Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions.Add((context.WorkflowRule.Id, context.ActionIndex, context.Phase));
            Contexts.Add(context);

            if (failFirstExecution && !_hasFailed)
            {
                _hasFailed = true;
                throw new InvalidOperationException("Expected test failure.");
            }

            return Task.FromResult(outputsByActionIndex is not null
                && outputsByActionIndex.TryGetValue(context.ActionIndex, out var outputValues)
                    ? ActionExecutionResult.FromOutput(outputValues)
                    : ActionExecutionResult.Completed);
        }
    }

    private sealed class AlwaysFailingActionExecutor : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public int Attempts { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Always fails.");
        }
    }

    private sealed class FailByPhaseActionExecutor : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public List<WorkflowExecutionPhase> Executions { get; } = [];

        public Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions.Add(context.Phase);
            throw new InvalidOperationException($"{context.Phase} failed.");
        }
    }

    private sealed class BlockingFirstActionExecutor : IWorkflowActionExecutor
    {
        private readonly TaskCompletionSource _releaseFirstAction = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ActionType => TestAction.TestActionType;
        public TaskCompletionSource FirstActionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Executions { get; } = [];

        public async Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions.Add(context.ActionIndex);
            if (context.ActionIndex is 0)
            {
                FirstActionStarted.TrySetResult();
                await _releaseFirstAction.Task.WaitAsync(cancellationToken);
            }

            return ActionExecutionResult.Completed;
        }

        public void ReleaseFirstAction()
        {
            _releaseFirstAction.TrySetResult();
        }
    }

    private sealed class LateBoundWorkflowRuleInvoker : IWorkflowRuleInvoker
    {
        public IWorkflowRuleInvoker? Inner { get; set; }

        public Task InvokeAsync(
            string workflowRuleId,
            IStreamEvent streamEvent,
            string invocationId,
            IReadOnlyDictionary<string, string>? args = null,
            CancellationToken cancellationToken = default)
        {
            return Inner!.InvokeAsync(workflowRuleId, streamEvent, invocationId, args, cancellationToken);
        }
    }

    private sealed class CancellationObservingActionExecutor : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public bool SawCancellation { get; private set; }

        public async Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
                throw;
            }

            return ActionExecutionResult.Completed;
        }
    }

    private sealed class CancelFirstExecutionActionExecutor(CancellationTokenSource cancellationTokenSource)
        : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public int Attempts { get; private set; }

        public Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Attempts is 1)
            {
                cancellationTokenSource.Cancel();
                throw new OperationCanceledException(cancellationTokenSource.Token);
            }

            return Task.FromResult(ActionExecutionResult.Completed);
        }
    }

    private sealed class ConcurrencyTrackingActionExecutor : IWorkflowActionExecutor
    {
        private int _currentConcurrency;
        public string ActionType => TestAction.TestActionType;
        public int MaxObservedConcurrency { get; private set; }

        public async Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, current);
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref _currentConcurrency);
            return ActionExecutionResult.Completed;
        }
    }

    private sealed class FakeWorkflowRuleQueryService(IReadOnlyCollection<WorkflowRule> rules) : IWorkflowRuleQueryService
    {
        public IReadOnlyCollection<WorkflowRule> Rules { get; set; } = rules;

        public Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(
            string eventTypeKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowRule>>(
                Rules.Where(rule => rule.EventTypeKey == eventTypeKey && rule.IsEnabled).ToArray());
        }

        public Task<IReadOnlyList<WorkflowRuleSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowRuleSummaryDto>>([]);
        }

        public Task<WorkflowRule?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Rules.FirstOrDefault(rule => rule.Id == id));
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    }
}
