using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Integration.EventBus;

public sealed class TdqReplayTests
{
    [Fact]
    public async Task Given_ChannelIsFull_When_EventIsPublished_Then_EventIsStoredInTdq()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var queue = new TransientDeliveryQueueStore(context);
        await using var bus = new InMemoryStreamEventBus(capacity: 1, overflowStore: queue);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Subscribe<UserSentMessageEvent>(async (_, _) =>
        {
            firstHandlerStarted.SetResult();
            await releaseFirstHandler.Task;
        });

        await bus.PublishAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        await firstHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await bus.PublishAsync(NewMessageEvent("user-2"), TestContext.Current.CancellationToken);
        await bus.PublishAsync(NewMessageEvent("user-3"), TestContext.Current.CancellationToken);

        var pending = await queue.GetPendingAsync(TestContext.Current.CancellationToken);

        pending.Should().ContainSingle()
            .Which.PayloadJson.Should().Contain("user-3");

        releaseFirstHandler.SetResult();
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_PendingTdqItem_When_Replayed_Then_EventIsPublishedAndItemIsDeleted()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var queue = new TransientDeliveryQueueStore(context);
        await queue.EnqueueAsync(NewMessageEvent("user-1"), TestContext.Current.CancellationToken);
        await using var bus = new InMemoryStreamEventBus();
        var received = new ConcurrentBag<string>();
        bus.Subscribe<UserSentMessageEvent>((streamEvent, _) =>
        {
            received.Add(streamEvent.User.UserId);
            return Task.CompletedTask;
        });
        var replayService = new TdqReplayService(queue, bus);

        await replayService.ReplayAsync(TestContext.Current.CancellationToken);

        received.Should().ContainSingle().Which.Should().Be("user-1");
        (await queue.GetPendingAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UserSentMessageEvent NewMessageEvent(string userId)
    {
        return new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", userId, $"User {userId}"),
        };
    }
}
