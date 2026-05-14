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
        return new WorkflowEngine(
            bus,
            new FakeWorkflowRuleQueryService(rules),
            new WorkflowConditionEvaluator(new FakeClock()),
            executors,
            new InMemoryWorkflowActionExecutionStore(),
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

    private static UserSentMessageEvent NewMessageEvent(string eventId = "event-1")
    {
        return new UserSentMessageEvent
        {
            EventId = eventId,
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            MessageText = "!hello",
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

    private sealed class RecordingActionExecutor(bool failFirstExecution = false) : IWorkflowActionExecutor
    {
        private bool _hasFailed;
        public string ActionType => TestAction.TestActionType;
        public List<(string RuleId, int ActionIndex)> Executions { get; } = [];

        public Task ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Executions.Add((context.WorkflowRule.Id, context.ActionIndex));

            if (failFirstExecution && !_hasFailed)
            {
                _hasFailed = true;
                throw new InvalidOperationException("Expected test failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysFailingActionExecutor : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public int Attempts { get; private set; }

        public Task ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Always fails.");
        }
    }

    private sealed class CancellationObservingActionExecutor : IWorkflowActionExecutor
    {
        public string ActionType => TestAction.TestActionType;
        public bool SawCancellation { get; private set; }

        public async Task ExecuteAsync(
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
        }
    }

    private sealed class ConcurrencyTrackingActionExecutor : IWorkflowActionExecutor
    {
        private int _currentConcurrency;
        public string ActionType => TestAction.TestActionType;
        public int MaxObservedConcurrency { get; private set; }

        public async Task ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, current);
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref _currentConcurrency);
        }
    }

    private sealed class FakeWorkflowRuleQueryService(IReadOnlyCollection<WorkflowRule> rules) : IWorkflowRuleQueryService
    {
        public Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(
            string eventTypeKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowRule>>(
                rules.Where(rule => rule.EventTypeKey == eventTypeKey && rule.IsEnabled).ToArray());
        }

        public Task<IReadOnlyList<WorkflowRuleSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowRuleSummaryDto>>([]);
        }

        public Task<WorkflowRule?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(rules.FirstOrDefault(rule => rule.Id == id));
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    }
}
