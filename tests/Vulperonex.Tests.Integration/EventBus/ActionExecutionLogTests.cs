using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Integration.EventBus;

public sealed class ActionExecutionLogTests
{
    [Fact]
    public async Task Given_ActionExecutionLog_When_StatusChanges_Then_CompletedFailedAndPendingCanBeQueried()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new ActionExecutionLogStore(context);

        await store.InsertPendingAsync("pending-key", TestContext.Current.CancellationToken);
        await store.InsertPendingAsync("completed-key", TestContext.Current.CancellationToken);
        await store.MarkCompletedAsync("completed-key", TestContext.Current.CancellationToken);
        await store.InsertPendingAsync("failed-key", TestContext.Current.CancellationToken);
        await store.MarkFailedAsync("failed-key", TestContext.Current.CancellationToken);

        (await store.FindAsync("pending-key", TestContext.Current.CancellationToken))!.Status.Should().Be(ActionExecutionStatus.Pending);
        (await store.FindAsync("completed-key", TestContext.Current.CancellationToken))!.Status.Should().Be(ActionExecutionStatus.Completed);
        (await store.FindAsync("failed-key", TestContext.Current.CancellationToken))!.Status.Should().Be(ActionExecutionStatus.Failed);
    }
}
