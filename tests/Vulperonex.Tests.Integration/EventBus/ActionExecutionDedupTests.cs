using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Time;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Integration.EventBus;

public sealed class ActionExecutionDedupTests
{
    [Fact]
    public async Task Given_CompletedExecution_When_SameKeyBeginsAgain_Then_ExecutionIsSkipped()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero));
        var store = new ActionExecutionLogStore(context, clock);

        var first = await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);
        await store.MarkCompletedAsync(first.DedupKey, TestContext.Current.CancellationToken);
        var second = await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);

        second.ShouldExecute.Should().BeFalse();
        second.Status.Should().Be(ActionExecutionStatus.Completed);
    }

    [Fact]
    public async Task Given_StalePendingExecution_When_BeginsAgain_Then_AttemptCountIncreases()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero));
        var store = new ActionExecutionLogStore(context, clock);

        await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(31));
        var retry = await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);

        retry.ShouldExecute.Should().BeTrue();
        retry.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_MaxRetriesExceeded_When_BeginsAgain_Then_ExecutionIsMarkedFailed()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero));
        var store = new ActionExecutionLogStore(context, clock, maxRetries: 1);

        await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(31));
        await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(31));
        var result = await store.BeginExecutionAsync("event-1:rule-1:0", TestContext.Current.CancellationToken);

        result.ShouldExecute.Should().BeFalse();
        result.Status.Should().Be(ActionExecutionStatus.Failed);
    }

    [Fact]
    public void Given_SubWorkflowKey_When_Composed_Then_InvocationIdIsIncluded()
    {
        var dedupKey = ActionExecutionKey.Compose(
            eventId: "event-1",
            workflowRuleId: "rule-1",
            actionIndex: 0,
            invocationId: "invocation-1");

        dedupKey.Should().Be("event-1:rule-1:0:invocation-1");
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan elapsed)
        {
            UtcNow += elapsed;
        }
    }
}
