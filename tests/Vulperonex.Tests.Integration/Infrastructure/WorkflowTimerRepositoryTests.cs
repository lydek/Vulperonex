using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Infrastructure.Workflows;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class WorkflowTimerRepositoryTests
{
    [Fact]
    public async Task Given_Timer_When_AddedAndListedDue_Then_OnlyEnabledDueTimersAreReturned()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new WorkflowTimerRepository(context);
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-due",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now.AddSeconds(-1),
            },
            TestContext.Current.CancellationToken);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-disabled",
                RuleId = "rule-2",
                IntervalSeconds = 30,
                IsEnabled = false,
                NextFireAt = now.AddSeconds(-1),
            },
            TestContext.Current.CancellationToken);

        var due = await repository.ListDueAsync(now, TestContext.Current.CancellationToken);

        due.Should().ContainSingle().Which.Id.Should().Be("timer-due");
    }

    [Fact]
    public async Task Given_Timer_When_MarkedFired_Then_NextFireAtIsPersisted()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new WorkflowTimerRepository(context);
        var now = new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(
            new WorkflowTimer
            {
                Id = "timer-1",
                RuleId = "rule-1",
                IntervalSeconds = 30,
                IsEnabled = true,
                NextFireAt = now,
            },
            TestContext.Current.CancellationToken);

        await repository.MarkFiredAsync("timer-1", now.AddSeconds(30), TestContext.Current.CancellationToken);

        var timer = await repository.GetAsync("timer-1", TestContext.Current.CancellationToken);
        timer.Should().NotBeNull();
        timer!.NextFireAt.Should().Be(now.AddSeconds(30));
    }
}
