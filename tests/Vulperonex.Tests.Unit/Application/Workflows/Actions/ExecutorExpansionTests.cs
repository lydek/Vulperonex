using FluentAssertions;
using Vulperonex.Application.Counters;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
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
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().ContainSingle().Which.Should().Be(PlatformIdentity.Create("twitch", "alice"));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Platform"].Should().Be("twitch");
        result.OutputValues!["UserId"].Should().Be("alice");
        result.OutputValues!["CheckInCount"].Should().Be(1);
    }

    [Fact]
    public async Task Given_AddLotteryTicketsAction_When_Executed_Then_CounterUsesLotteryTicketKey()
    {
        var repository = new RecordingCounterRepository();
        var executor = new AddLotteryTicketsActionExecutor(repository, new TemplateResolver());

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

    private static ActionExecutionContext NewContext()
    {
        var streamEvent = new UserSentMessageEvent
        {
            EventId = "event-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
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

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    }
}
