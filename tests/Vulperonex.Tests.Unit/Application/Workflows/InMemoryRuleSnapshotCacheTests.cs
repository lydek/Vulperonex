using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Application.Workflows.Dtos;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows;

public sealed class InMemoryRuleSnapshotCacheTests
{
    [Fact]
    public async Task Given_RuleIsCached_When_SourceChangesWithoutInvalidation_Then_SnapshotDoesNotChange()
    {
        var queryService = new MutableWorkflowRuleQueryService(
        [
            NewRule("rule-1", actions: [new TestAction()]),
        ]);
        var cache = new InMemoryRuleSnapshotCache(queryService);

        var first = await cache.GetByEventTypeAsync(StreamEventKeys.UserSentMessage, TestContext.Current.CancellationToken);
        queryService.Rules =
        [
            NewRule("rule-1", actions: [new TestAction(), new TestAction()]),
        ];
        var second = await cache.GetByEventTypeAsync(StreamEventKeys.UserSentMessage, TestContext.Current.CancellationToken);

        first.Should().ContainSingle().Which.Actions.Should().ContainSingle();
        second.Should().ContainSingle().Which.Actions.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_CacheInvalidated_When_SourceChanges_Then_NextFetchUsesNewSnapshot()
    {
        var queryService = new MutableWorkflowRuleQueryService(
        [
            NewRule("rule-1", actions: [new TestAction()]),
        ]);
        var cache = new InMemoryRuleSnapshotCache(queryService);
        await cache.GetByEventTypeAsync(StreamEventKeys.UserSentMessage, TestContext.Current.CancellationToken);

        queryService.Rules =
        [
            NewRule("rule-1", actions: [new TestAction(), new TestAction()]),
        ];
        cache.Invalidate("rule-1");
        var updated = await cache.GetByEventTypeAsync(StreamEventKeys.UserSentMessage, TestContext.Current.CancellationToken);

        updated.Should().ContainSingle().Which.Actions.Should().HaveCount(2);
    }

    private static WorkflowRule NewRule(string id, IReadOnlyList<WorkflowAction> actions)
    {
        return new WorkflowRule
        {
            Id = id,
            Name = id,
            EventTypeKey = StreamEventKeys.UserSentMessage,
            Actions = actions,
        };
    }

    private sealed record TestAction : WorkflowAction
    {
        public override string Type => "test";
    }

    private sealed class MutableWorkflowRuleQueryService(IReadOnlyCollection<WorkflowRule> rules)
        : IWorkflowRuleQueryService
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
}
