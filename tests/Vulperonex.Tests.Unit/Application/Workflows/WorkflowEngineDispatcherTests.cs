using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Application.Workflows.Filters;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows;

public sealed class WorkflowEngineDispatcherTests
{
    [Fact]
    public async Task Given_WorkflowActionIsDelayed_When_NextChatEventArrives_Then_BusStreamIsNotBlocked()
    {
        await using var bus = new InMemoryStreamEventBus();
        var blockingExecutor = new BlockingActionExecutor();
        var rule = NewRule();
        var services = new ServiceCollection()
            .AddScoped(_ => NewEngine(bus, [rule], [blockingExecutor]))
            .BuildServiceProvider();
        var dispatcher = new WorkflowEngineDispatcher(
            bus,
            new EnabledModuleStateService(),
            new NoopSettingObservable(),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkflowEngineDispatcher>.Instance);
        var secondEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bus.Events
            .OfType<UserSentMessageEvent>()
            .Subscribe(streamEvent =>
            {
                if (streamEvent.User.UserId == "bob")
                {
                    secondEventReceived.TrySetResult();
                }
            });

        await dispatcher.StartAsync(TestContext.Current.CancellationToken);
        await bus.PublishAsync(NewMessageEvent("alice"), TestContext.Current.CancellationToken);
        await blockingExecutor.FirstActionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await bus.PublishAsync(NewMessageEvent("bob"), TestContext.Current.CancellationToken);

        await secondEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        blockingExecutor.Release();
        await dispatcher.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WorkflowEngine NewEngine(
        IStreamEventBus bus,
        IReadOnlyCollection<WorkflowRule> rules,
        IEnumerable<IWorkflowActionExecutor> executors)
    {
        return new WorkflowEngine(
            bus,
            new InMemoryRuleSnapshotCache(new FakeWorkflowRuleQueryService(rules)),
            new WorkflowConditionEvaluator(new FakeClock()),
            executors,
            new InMemoryWorkflowActionExecutionStore(),
            new NCalcExpressionEvaluator(NullLogger<NCalcExpressionEvaluator>.Instance),
            new InMemoryWorkflowThrottleService(new FakeClock()),
            new FakeClock(),
            NullLogger<WorkflowEngine>.Instance,
            WorkflowEngineTests.NewMatcherRegistry());
    }

    private static WorkflowRule NewRule()
    {
        return new WorkflowRule
        {
            Id = "rule-1",
            Name = "rule-1",
            EventTypeKey = StreamEventKeys.UserSentMessage,
            IsEnabled = true,
            Actions = [new BlockingAction()],
        };
    }

    private static UserSentMessageEvent NewMessageEvent(string userId)
    {
        return new UserSentMessageEvent
        {
            EventId = $"event-{userId}",
            Platform = "twitch",
            User = new StreamUser("twitch", userId, userId),
            MessageText = "!checkin",
        };
    }

    private sealed record BlockingAction : WorkflowAction
    {
        public const string BlockingActionType = "blocking";
        public override string Type => BlockingActionType;
    }

    private sealed class BlockingActionExecutor : IWorkflowActionExecutor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ActionType => BlockingAction.BlockingActionType;
        public TaskCompletionSource FirstActionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ActionExecutionResult> ExecuteAsync(
            WorkflowAction action,
            ActionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            FirstActionStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return ActionExecutionResult.Completed;
        }

        public void Release()
        {
            _release.TrySetResult();
        }
    }

    private sealed class EnabledModuleStateService : IModuleStateService
    {
        public Task<IReadOnlyList<ModuleStateSnapshot>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleStateSnapshot>>([]);

        public Task<bool> IsEnabledAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ModuleToggleResult> ToggleAsync(
            string moduleName,
            bool enabled,
            string actorKind,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NoopSettingObservable : IObservable<SettingChangedEvent>
    {
        public IDisposable Subscribe(IObserver<SettingChangedEvent> observer)
        {
            return new NoopDisposable();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
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
