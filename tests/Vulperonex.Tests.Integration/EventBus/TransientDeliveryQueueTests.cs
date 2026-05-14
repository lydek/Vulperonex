using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Integration.EventBus;

public sealed class TransientDeliveryQueueTests
{
    [Fact]
    public async Task Given_TdqItem_When_Enqueued_Then_SamePayloadCanBeReadBack()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var queue = new TransientDeliveryQueueStore(context);

        var item = await queue.EnqueueAsync(
            eventType: "workflow.invoke_subworkflow",
            payloadJson: """{"InvocationId":"invocation-1","Value":42}""",
            TestContext.Current.CancellationToken);

        var pending = await queue.GetPendingAsync(TestContext.Current.CancellationToken);

        pending.Should().ContainSingle().Which.Should().BeEquivalentTo(item);
        pending[0].PayloadJson.Should().Contain("invocation-1");
    }
}
