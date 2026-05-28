using FluentAssertions;
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
using Vulperonex.Plugins.Abstractions;
using Xunit;

namespace Vulperonex.Tests.Unit.Plugins;

public sealed class PluginWorkflowTests
{
    [Fact]
    public async Task Given_PluginPublishesCustomEvent_When_WorkflowRuleMatches_Then_ChatActionExecutes()
    {
        await using var bus = new InMemoryStreamEventBus();
        var registry = new InMemoryStreamEventTypeRegistry();
        var plugin = new CustomEventPlugin();
        await plugin.InitializeAsync(
            new PluginContext(bus, new PluginEventTypeRegistrar(registry)),
            TestContext.Current.CancellationToken);
        var sender = new RecordingChatSender("plugin");
        var rule = new WorkflowRule
        {
            Id = "custom-rule",
            Name = "Custom Rule",
            EventTypeKey = CustomEventPlugin.CustomEventKey,
            Actions =
            [
                new SendChatMessageAction
                {
                    TargetPlatform = "plugin",
                    Template = "custom {event.type}",
                },
            ],
        };
        await using var engine = new WorkflowEngine(
            bus,
            new InMemoryRuleSnapshotCache(new FakeWorkflowRuleQueryService([rule])),
            new WorkflowConditionEvaluator(new FakeClock()),
            [new SendChatMessageActionExecutor([sender], new TemplateRenderer())],
            new InMemoryWorkflowActionExecutionStore(),
            new NCalcExpressionEvaluator(Microsoft.Extensions.Logging.Abstractions.NullLogger<NCalcExpressionEvaluator>.Instance),
            new InMemoryWorkflowThrottleService(new FakeClock()),
            new FakeClock(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowEngine>.Instance,
            Vulperonex.Tests.Unit.Application.Workflows.WorkflowEngineTests.NewMatcherRegistry());
        await engine.StartAsync(TestContext.Current.CancellationToken);

        registry.IsKnownForWorkflow(CustomEventPlugin.CustomEventKey).Should().BeTrue();
        await plugin.PublishCustomEventAsync(TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        sender.Messages.Should().ContainSingle().Which.Should().Be("custom custom.event");
    }

    private sealed class CustomEventPlugin : IVulperonexPlugin
    {
        public const string CustomEventKey = "custom.event";
        private IPluginContext? _context;
        public string Name => "custom-plugin";

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            _context = context;
            context.EventTypes.Register(new PluginEventTypeMetadata(CustomEventKey, "Custom plugin event"));
            return Task.CompletedTask;
        }

        public Task ExecuteActionAsync(
            string actionId,
            IPluginActionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishCustomEventAsync(CancellationToken cancellationToken)
        {
            return _context!.Events.PublishAsync(new CustomEvent(), cancellationToken);
        }
    }

    private sealed record CustomEvent : IStreamEvent
    {
        public string EventId { get; init; } = "custom-event-1";
        public string EventTypeKey => CustomEventPlugin.CustomEventKey;
        public string Platform { get; init; } = "plugin";
        public StreamUser? User { get; init; }
        public DateTimeOffset OccurredAt { get; init; } = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
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
