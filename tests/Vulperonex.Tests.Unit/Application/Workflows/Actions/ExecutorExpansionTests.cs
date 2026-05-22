using FluentAssertions;
using Vulperonex.Application.Counters;
using Vulperonex.Application.Members;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Expressions;
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

    [Fact]
    public async Task Given_UpdateCounterAction_When_Executed_Then_OutputContainsNewValue()
    {
        var repository = new RecordingCounterRepository();
        var executor = new UpdateCounterActionExecutor(repository, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new UpdateCounterAction
            {
                Key = "lottery.tickets.{Member.UserId}",
                Delta = 3,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("lottery.tickets.alice", 3));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Key"].Should().Be("lottery.tickets.alice");
        result.OutputValues!["Value"].Should().Be(3);
    }

    [Fact]
    public async Task Given_TriggerCheckInAction_When_Executed_Then_OutputContainsCheckInCount()
    {
        var repository = new RecordingMemberStreamStateRepository();
        var executor = new TriggerCheckInActionExecutor(repository, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new TriggerCheckInAction { UserId = "{Member.UserId}" },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.CheckIns.Should().ContainSingle().Which.Should().Be(PlatformIdentity.Create("twitch", "alice"));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Platform"].Should().Be("twitch");
        result.OutputValues!["UserId"].Should().Be("alice");
        result.OutputValues!["CheckInCount"].Should().Be(1);
    }

    [Fact]
    public async Task Given_AddLotteryTicketsAction_When_Executed_Then_CounterUsesLotteryTicketKey()
    {
        var repository = new RecordingCounterRepository();
        var executor = new AddLotteryTicketsActionExecutor(repository, new TemplateResolver());

        var result = await executor.ExecuteAsync(
            new AddLotteryTicketsAction
            {
                UserId = "{Member.UserId}",
                Amount = 5,
            },
            NewContext(),
            TestContext.Current.CancellationToken);

        repository.Calls.Should().ContainSingle().Which.Should().Be(("lottery.tickets.alice", 5));
        result.OutputValues.Should().NotBeNull();
        result.OutputValues!["Key"].Should().Be("lottery.tickets.alice");
        result.OutputValues!["UserId"].Should().Be("alice");
        result.OutputValues!["TicketsAdded"].Should().Be(5);
        result.OutputValues!["TicketCount"].Should().Be(5);
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
            ActionIndex: 0,
            ExpressionContext: new Vulperonex.Application.Expressions.ExpressionContext(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["UserId"] = "alice",
                }));
    }

    private sealed class RecordingCounterRepository : ICounterRepository
    {
        public List<(string Key, long Delta)> Calls { get; } = [];

        public Task<long> IncrementAsync(string key, long delta, CancellationToken cancellationToken = default)
        {
            Calls.Add((key, delta));
            return Task.FromResult(delta);
        }
    }

    private sealed class RecordingMemberStreamStateRepository : IMemberStreamStateRepository
    {
        public List<PlatformIdentity> CheckIns { get; } = [];

        public Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> IncrementCheckInAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            CheckIns.Add(identity);
            return Task.FromResult(CheckIns.Count);
        }
    }
}
