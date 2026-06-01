using FluentAssertions;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.EventTypes;
using Vulperonex.Application.Time;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Conditions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Application.Workflows.Filters;
using Vulperonex.Application.Workflows.Filters.Matchers;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.EventTypes;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Integration.Adapters;

/// <summary>
/// Phase 8 Final Checkpoint: a typed <c>CommandName</c> trigger filter must fire
/// end-to-end through the real engine + matcher dispatch, and must enforce the
/// word boundary that eradicates the original §1 bug (<c>!so</c> matching
/// <c>!sorry</c>). Drives the engine through the SimulationAdapter — the same
/// publish path the admin Simulate page uses.
/// </summary>
public sealed class WorkflowCommandFilterTests
{
    [Theory]
    [InlineData("!so", true)]          // exact command
    [InlineData("!so target", true)]   // command + argument
    [InlineData("!sorry", false)]      // §1 bug: must NOT match
    [InlineData("!sorry I missed it", false)]
    [InlineData("!Solid", false)]
    [InlineData("hello", false)]
    public async Task Given_CommandNameFilter_When_MessageSimulated_Then_FiresOnlyOnWordBoundaryMatch(
        string message, bool shouldFire)
    {
        await using var bus = new InMemoryStreamEventBus();
        var sender = new RecordingChatSender("twitch");
        await using var engine = NewEngine(bus, sender);
        await engine.StartAsync();

        var adapter = new SimulationAdapter(bus, new InMemoryStreamEventTypeRegistry());
        await adapter.SimulateAsync(
            SimulationRequest.Message("twitch", new StreamUser("twitch", "alice", "Alice"), message),
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        if (shouldFire)
        {
            sender.Messages.Should().ContainSingle().Which.Should().Be($"checked in: {message}");
        }
        else
        {
            sender.Messages.Should().BeEmpty();
        }
    }

    private static WorkflowEngine NewEngine(IStreamEventBus bus, RecordingChatSender sender)
    {
        var rule = new WorkflowRule
        {
            Id = "rule-checkin",
            Name = "checkin",
            EventTypeKey = StreamEventKeys.UserSentMessage,
            Trigger = new WorkflowTrigger(new Dictionary<string, string> { ["CommandName"] = "!so" }),
            Actions = [new SendChatMessageAction { Template = "checked in: {event.message}" }],
        };

        var matcherRegistry = new TriggerFilterMatcherRegistry(
            new ITriggerFilterMatcher[]
            {
                new MatchChatMessage(),
                new MatchUserDonated(),
                new MatchUserSubscribed(),
                new MatchUserGiftedSub(),
                new MatchChannelRaided(),
                new MatchRewardRedeemed(),
                new MatchWorkflowTimer(),
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TriggerFilterMatcherRegistry>.Instance);

        return new WorkflowEngine(
            bus,
            new InMemoryRuleSnapshotCache(new FakeWorkflowRuleQueryService([rule])),
            new WorkflowConditionEvaluator(new FakeClock()),
            [new SendChatMessageActionExecutor([sender], new TemplateResolver(), new TemplateRenderer())],
            new InMemoryWorkflowActionExecutionStore(),
            new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance),
            new InMemoryWorkflowThrottleService(new FakeClock()),
            new FakeClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowEngine>.Instance,
            matcherRegistry);
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
