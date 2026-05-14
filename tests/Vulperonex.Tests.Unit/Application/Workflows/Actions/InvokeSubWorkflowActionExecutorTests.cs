using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Actions;

public sealed class InvokeSubWorkflowActionExecutorTests
{
    [Fact]
    public async Task Given_InvokeSubWorkflowAction_When_Executed_Then_TargetWorkflowIsInvokedWithStableInvocationId()
    {
        var invoker = new RecordingWorkflowRuleInvoker();
        var executor = new InvokeSubWorkflowActionExecutor(invoker);
        var streamEvent = new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };
        var context = new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = "parent", Name = "Parent", EventTypeKey = StreamEventKeys.UserSentMessage },
            ActionIndex: 0,
            InvocationId: "stable-invocation");

        await executor.ExecuteAsync(
            new InvokeSubWorkflowAction { WorkflowId = "child" },
            context,
            TestContext.Current.CancellationToken);

        invoker.Invocations.Should().ContainSingle()
            .Which.Should().Be(("child", "event:" + streamEvent.EventId, "stable-invocation"));
    }

    private sealed class RecordingWorkflowRuleInvoker : IWorkflowRuleInvoker
    {
        public List<(string WorkflowRuleId, string EventId, string InvocationId)> Invocations { get; } = [];

        public Task InvokeAsync(
            string workflowRuleId,
            IStreamEvent streamEvent,
            string invocationId,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((workflowRuleId, "event:" + streamEvent.EventId, invocationId));
            return Task.CompletedTask;
        }
    }
}
