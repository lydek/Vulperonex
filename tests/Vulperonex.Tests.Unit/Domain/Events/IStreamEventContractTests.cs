using FluentAssertions;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Events;

public sealed class IStreamEventContractTests
{
    [Fact]
    public void Given_StreamEventImplementation_When_ReadThroughInterface_Then_EventContractExposesCanonicalProperties()
    {
        var occurredAt = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var user = new StreamUser("twitch", "12345", "alice");
        IStreamEvent streamEvent = new TestStreamEvent(
            EventId: "event-1",
            EventTypeKey: "user.message",
            Platform: "twitch",
            User: user,
            OccurredAt: occurredAt);

        streamEvent.EventId.Should().Be("event-1");
        streamEvent.EventTypeKey.Should().Be("user.message");
        streamEvent.Platform.Should().Be("twitch");
        streamEvent.User.Should().Be(user);
        streamEvent.OccurredAt.Should().Be(occurredAt);
    }

    private sealed record TestStreamEvent(
        string EventId,
        string EventTypeKey,
        string Platform,
        StreamUser? User,
        DateTimeOffset OccurredAt) : IStreamEvent;
}
