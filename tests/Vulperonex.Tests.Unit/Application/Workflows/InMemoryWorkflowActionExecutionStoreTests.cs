using FluentAssertions;
using Vulperonex.Application.Workflows;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows;

public sealed class InMemoryWorkflowActionExecutionStoreTests
{
    [Fact]
    public async Task Given_KeyIsInFlight_When_TryBeginIsCalledAgain_Then_SecondCallIsRejected()
    {
        var store = new InMemoryWorkflowActionExecutionStore();
        var key = new ActionExecutionKey("event-1", "rule-1", 0);

        var firstBegin = await store.TryBeginAsync(key, TestContext.Current.CancellationToken);
        var secondBegin = await store.TryBeginAsync(key, TestContext.Current.CancellationToken);

        firstBegin.Should().BeTrue();
        secondBegin.Should().BeFalse();
    }
}
