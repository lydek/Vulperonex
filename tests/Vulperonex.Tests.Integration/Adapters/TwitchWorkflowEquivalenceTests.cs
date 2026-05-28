using FluentAssertions;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Adapters.Twitch;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Integration.Adapters;

public sealed class TwitchWorkflowEquivalenceTests
{
    [Fact]
    public async Task Given_EquivalentSimulationAndTwitchMessage_When_Published_Then_WorkflowSideEffectsMatch()
    {
        var simulationMessages = await RunSimulationAsync();
        var twitchMessages = await RunTwitchAsync();

        twitchMessages.Should().Equal(simulationMessages);
    }

    private static async Task<IReadOnlyList<string>> RunSimulationAsync()
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        await using var engine = NewEngine(bus, sender);
        await engine.StartAsync();
        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());

        await adapter.SimulateAsync(
            SimulationRequest.Message("twitch", new StreamUser("twitch", "alice", "Alice"), "!hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
        return sender.Messages;
    }

    private static async Task<IReadOnlyList<string>> RunTwitchAsync()
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        await using var engine = NewEngine(bus, sender);
        await engine.StartAsync();
        var adapter = new TwitchAdapter(bus, new InMemoryStreamEventTypeRegistry());
        await adapter.StartAsync(TestContext.Current.CancellationToken);

        await adapter.PublishMockPayloadAsync(
            new TwitchMockPayload(TwitchMockPayloadKind.Message, new StreamUser("twitch", "alice", "Alice"), MessageText: "!hello"),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
        return sender.Messages;
    }

    private static WorkflowEngine NewEngine(IStreamEventBus bus, RecordingChatSender sender)
    {
        var rule = new WorkflowRule
        {
            Id = "rule-1",
            Name = "echo",
            EventTypeKey = StreamEventKeys.UserSentMessage,
            Actions = [new SendChatMessageAction { Template = "Echo {event.message}" }],
        };

        return new WorkflowEngine(
            bus,
            new InMemoryRuleSnapshotCache(new FakeWorkflowRuleQueryService([rule])),
            new WorkflowConditionEvaluator(new FakeClock()),
            [new SendChatMessageActionExecutor([sender], new TemplateRenderer())],
            new InMemoryWorkflowActionExecutionStore(),
            new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance),
            new InMemoryWorkflowThrottleService(new FakeClock()),
            new FakeClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowEngine>.Instance);
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

    private sealed class FakeWorkflowRuleQueryService(IReadOnlyCollection<WorkflowRule> rules) : IWorkflowRuleQueryService
    {
        public Task<IReadOnlyList<WorkflowRule>> ListEnabledByEventTypeAsync(string eventTypeKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkflowRule>>(rules.Where(rule => rule.EventTypeKey == eventTypeKey).ToArray());
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
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 16, 0, 0, 0, TimeSpan.Zero);
    }
}
