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
        using var module = new MemberModule(bus, resolver);
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

    private sealed class RecordingMemberResolver : IMemberResolver
    {
        public List<PlatformIdentity> Identities { get; } = [];

        public Task<string> ResolveMemberIdAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
        {
            Identities.Add(identity);
            return Task.FromResult("01HX0000000000000000000000");
        }
    }
}
