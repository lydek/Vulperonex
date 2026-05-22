using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
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

    private static ActionExecutionContext NewContext()
    {
        var streamEvent = new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };

        return new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "rule-1", Name = "Rule", EventTypeKey = StreamEventKeys.UserSentMessage },
            ActionIndex: 0);
    }
}
