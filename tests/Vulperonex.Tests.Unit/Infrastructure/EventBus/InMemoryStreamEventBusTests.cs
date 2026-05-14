using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.EventBus;

public sealed class InMemoryStreamEventBusTests
{
    [Fact]
    public async Task Given_HandlerThrows_When_PublishingEvents_Then_OtherHandlersStillReceiveEvents()
    {
        await using var bus = new InMemoryStreamEventBus();
        var received = new ConcurrentBag<string>();

        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            if (streamEvent.User.UserId == "user-3")
            {
                throw new InvalidOperationException("Handler failed.");
            }

            return Task.CompletedTask;
        });
        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent.User.UserId);
            return Task.CompletedTask;
        });

        for (var index = 1; index <= 5; index++)
        {
            await bus.PublishAsync(NewMessageEvent($"user-{index}"), TestContext.Current.CancellationToken);
        }

        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().BeEquivalentTo("user-1", "user-2", "user-3", "user-4", "user-5");
    }

    [Fact]
    public async Task Given_StreamEventSubscription_When_ConcreteEventIsPublished_Then_SubscriptionReceivesIt()
    {
        await using var bus = new InMemoryStreamEventBus();
        var received = new ConcurrentBag<IStreamEvent>();
        var streamEvent = NewMessageEvent("user-1");

        bus.Subscribe<IStreamEvent>((publishedEvent, _) =>
        {
            received.Add(publishedEvent);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().ContainSingle().Which.Should().Be(streamEvent);
    }

    [Fact]
    public async Task Given_ConcreteEventSubscription_When_DifferentEventTypeIsPublished_Then_SubscriptionDoesNotReceiveIt()
    {
        await using var bus = new InMemoryStreamEventBus();
        var received = new ConcurrentBag<UserSentMessageEvent>();

        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(
            new UserFollowedEvent { Platform = "twitch", User = NewUser("user-1") },
            TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_SlowHandler_When_EventIsPublished_Then_PublishDoesNotWaitForHandlerCompletion()
    {
        await using var bus = new InMemoryStreamEventBus();
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Subscribe<UserSentMessageEvent>(async (_, _) =>
        {
            handlerStarted.SetResult();
            await releaseHandler.Task;
        });

        var stopwatch = Stopwatch.StartNew();
        await bus.PublishAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(10));

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        releaseHandler.SetResult();
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_PublishedEvent_When_WaitingForIdle_Then_WaitCompletesAfterHandlerCompletes()
    {
        await using var bus = new InMemoryStreamEventBus();
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Subscribe<UserSentMessageEvent>(async (_, _) =>
        {
            handlerStarted.SetResult();
            await releaseHandler.Task;
        });

        await bus.PublishAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var waitForIdleTask = bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
        await Task.Yield();
        waitForIdleTask.IsCompleted.Should().BeFalse();

        releaseHandler.SetResult();
        await waitForIdleTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_HandlerThrows_When_WaitingForIdle_Then_WaitStillCompletes()
    {
        await using var bus = new InMemoryStreamEventBus();

        bus.Subscribe<UserSentMessageEvent>((_, _) => throw new InvalidOperationException("Handler failed."));

        await bus.PublishAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        var act = async () => await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Given_WaitForIdleCancellation_When_HandlerIsStillRunning_Then_WaitIsCanceled()
    {
        await using var bus = new InMemoryStreamEventBus();
        using var cancellationTokenSource = new CancellationTokenSource();
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Subscribe<UserSentMessageEvent>(async (_, _) =>
        {
            handlerStarted.SetResult();
            await releaseHandler.Task;
        });

        await bus.PublishAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        var waitForIdleTask = bus.WaitForIdleAsync(cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();

        await FluentActions.Awaiting(() => waitForIdleTask).Should().ThrowAsync<OperationCanceledException>();

        releaseHandler.SetResult();
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
    }

    private static UserSentMessageEvent NewMessageEvent(string userId)
    {
        return new UserSentMessageEvent
        {
            Platform = "twitch",
            User = NewUser(userId),
        };
    }

    private static StreamUser NewUser(string userId)
    {
        return new StreamUser("twitch", userId, $"User {userId}");
    }
}
