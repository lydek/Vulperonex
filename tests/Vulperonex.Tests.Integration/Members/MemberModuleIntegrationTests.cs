using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventBus;
using Vulperonex.Infrastructure.Members;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class MemberModuleIntegrationTests
{
    [Fact]
    public async Task Given_UserSubscribedEvent_When_Published_Then_PlatformIdentityStateIsUpdated()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(
            bus,
            new MemberResolver(context),
            new MemberStreamStateRepository(context));
        module.Start();

        await bus.PublishAsync(new UserSubscribedEvent
        {
            EventId = "sub-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            Tier = "1000",
        }, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        var identity = await context.PlatformIdentities.SingleAsync(TestContext.Current.CancellationToken);
        identity.IsSubscriber.Should().BeTrue();
        identity.SubscriptionTier.Should().Be("1000");
    }

    [Fact]
    public async Task Given_ReplayedFollowEvent_When_Published_Then_OnlyOneMemberIdentityIsCreated()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var bus = new InMemoryStreamEventBus();
        using var module = new MemberModule(
            bus,
            new MemberResolver(context),
            new MemberStreamStateRepository(context));
        module.Start();
        var streamEvent = new UserFollowedEvent
        {
            EventId = "follow-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
        };

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        var identity = await context.PlatformIdentities.SingleAsync(TestContext.Current.CancellationToken);
        identity.IsFollower.Should().BeTrue();
        (await context.Members.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Given_StreamStateUpdatedBeforeIdentityIsResolved_When_Updated_Then_ItFailsLoudly()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new MemberStreamStateRepository(context);

        var update = () => repository.MarkFollowerAsync(
            Domain.Members.PlatformIdentity.Create("twitch", "missing"),
            TestContext.Current.CancellationToken);

        await update.Should().ThrowAsync<InvalidOperationException>();
    }
}
