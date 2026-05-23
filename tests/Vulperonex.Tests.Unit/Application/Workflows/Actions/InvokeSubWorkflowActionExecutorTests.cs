using FluentAssertions;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.Expressions;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Actions;

public sealed class InvokeSubWorkflowActionExecutorTests
{
    [Fact]
    public async Task Given_InvokeSubWorkflowAction_When_Executed_Then_TargetWorkflowIsInvokedWithStableInvocationId()
    {
        var invoker = new RecordingWorkflowRuleInvoker();
        var executor = new InvokeSubWorkflowActionExecutor(() => invoker, new TemplateResolver());
        var streamEvent = NewEvent(eventId: "event-1");
        var context = NewContext(streamEvent, parentRuleId: "parent", actionIndex: 0);

        await executor.ExecuteAsync(
            new InvokeSubWorkflowAction { WorkflowId = "child" },
            context,
            TestContext.Current.CancellationToken);

        invoker.Invocations.Should().ContainSingle();
        invoker.Invocations[0].WorkflowRuleId.Should().Be("child");
        invoker.Invocations[0].EventId.Should().Be("event-1");
        invoker.Invocations[0].InvocationId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task Given_SameEventRuleAndActionIndex_When_ExecutedTwice_Then_InvocationIdIsDeterministic()
    {
        var invoker = new RecordingWorkflowRuleInvoker();
        var executor = new InvokeSubWorkflowActionExecutor(() => invoker, new TemplateResolver());
        var streamEvent = NewEvent(eventId: "event-1");
        var context = NewContext(streamEvent, parentRuleId: "parent", actionIndex: 0);
        var action = new InvokeSubWorkflowAction { WorkflowId = "child" };

        await executor.ExecuteAsync(action, context, TestContext.Current.CancellationToken);
        await executor.ExecuteAsync(action, context, TestContext.Current.CancellationToken);

        invoker.Invocations.Should().HaveCount(2);
        invoker.Invocations[0].InvocationId.Should().Be(invoker.Invocations[1].InvocationId);
    }

    [Fact]
    public async Task Given_DifferentActionIndex_When_Executed_Then_InvocationIdDiffers()
    {
        var invoker = new RecordingWorkflowRuleInvoker();
        var executor = new InvokeSubWorkflowActionExecutor(() => invoker, new TemplateResolver());
        var streamEvent = NewEvent(eventId: "event-1");
        var firstContext = NewContext(streamEvent, parentRuleId: "parent", actionIndex: 0);
        var secondContext = NewContext(streamEvent, parentRuleId: "parent", actionIndex: 1);
        var action = new InvokeSubWorkflowAction { WorkflowId = "child" };

        await executor.ExecuteAsync(action, firstContext, TestContext.Current.CancellationToken);
        await executor.ExecuteAsync(action, secondContext, TestContext.Current.CancellationToken);

        invoker.Invocations[0].InvocationId.Should().NotBe(invoker.Invocations[1].InvocationId);
    }

    private static UserSentMessageEvent NewEvent(string eventId)
    {
        return new UserSentMessageEvent
        {
            EventId = eventId,
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };
    }

    private static ActionExecutionContext NewContext(IStreamEvent streamEvent, string parentRuleId, int actionIndex)
    {
        return new ActionExecutionContext(
            streamEvent,
            new WorkflowRule { Id = parentRuleId, Name = parentRuleId, EventTypeKey = StreamEventKeys.UserSentMessage },
            actionIndex);
    }

    private sealed class RecordingWorkflowRuleInvoker : IWorkflowRuleInvoker
    {
        public List<(string WorkflowRuleId, string EventId, string InvocationId, IReadOnlyDictionary<string, string>? Args)> Invocations { get; } = [];

        public Task InvokeAsync(
            string workflowRuleId,
            IStreamEvent streamEvent,
            string invocationId,
            IReadOnlyDictionary<string, string>? args = null,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((workflowRuleId, streamEvent.EventId, invocationId, args));
            return Task.CompletedTask;
        }
    }
}
