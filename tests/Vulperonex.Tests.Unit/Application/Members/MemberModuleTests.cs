using FluentAssertions;
using Vulperonex.Application.Members;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.EventBus;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Members;

public sealed class MemberModuleTests
{
    [Fact]
    public async Task Given_UserSentMessageEvent_When_Published_Then_MemberResolverCreatesPlatformIdentity()
    {
        await using var bus = new InMemoryStreamEventBus();
        var resolver = new RecordingMemberResolver();
        using var module = new MemberModule(bus, resolver, new RecordingMemberStreamStateRepository());
        module.Start();

        await bus.PublishAsync(new UserSentMessageEvent
        {
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            MessageText = "hello",
        }, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        resolver.Identities.Should().ContainSingle().Subject.Should().Be(PlatformIdentity.Create("twitch", "alice"));
    }

    [Fact]
    public async Task Given_SubscriptionEvent_When_PublishedTwice_Then_StateUpdateIsAppliedOnceBySourceEventId()
    {
        await using var bus = new InMemoryStreamEventBus();
        var resolver = new RecordingMemberResolver();
        var states = new RecordingMemberStreamStateRepository();
        using var module = new MemberModule(bus, resolver, states);
        module.Start();
        var streamEvent = new UserSubscribedEvent
        {
            EventId = "event-1",
            Platform = "twitch",
            User = new StreamUser("twitch", "alice", "Alice"),
            Tier = "1000",
        };

        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.PublishAsync(streamEvent, TestContext.Current.CancellationToken);
        await bus.WaitForIdleAsync(TestContext.Current.CancellationToken);

        resolver.Identities.Should().ContainSingle();
        states.Subscriptions.Should().ContainSingle().Which.Should().Be((PlatformIdentity.Create("twitch", "alice"), "1000"));
    }

    private sealed class RecordingMemberResolver : IMemberResolver
    {
        public List<PlatformIdentity> Identities { get; } = [];

        public Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            Identities.Add(identity);
            return Task.FromResult("01HX0000000000000000000000");
        }
    }

    private sealed class RecordingMemberStreamStateRepository : IMemberStreamStateRepository
    {
        public List<PlatformIdentity> Followers { get; } = [];
        public List<(PlatformIdentity Identity, string Tier)> Subscriptions { get; } = [];

        public Task MarkFollowerAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            Followers.Add(identity);
            return Task.CompletedTask;
        }

        public Task MarkSubscriberAsync(PlatformIdentity identity, string tier, CancellationToken cancellationToken = default)
        {
            Subscriptions.Add((identity, tier));
            return Task.CompletedTask;
        }
    }
}
