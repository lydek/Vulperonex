using FluentAssertions;
using Vulperonex.Application.EventTypes;
using Vulperonex.Domain.Events;
using Vulperonex.Infrastructure.EventTypes;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.EventTypes;

public sealed class InMemoryStreamEventTypeRegistryTests
{
    [Fact]
    public void Given_DuplicateEventTypeKey_When_RegisteringTwice_Then_OnlyOneMetadataEntryIsKept()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var metadata = new StreamEventTypeMetadata(StreamEventKeys.UserSentMessage, "User sent message");

        registry.Register(metadata);
        registry.Register(metadata);

        registry.GetAll().Should().ContainSingle()
            .Which.Should().Be(metadata);
    }

    [Fact]
    public void Given_ConflictingMetadata_When_RegisteringSameKey_Then_FirstMetadataWins()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var first = new StreamEventTypeMetadata(StreamEventKeys.UserSentMessage, "Original description");
        var second = new StreamEventTypeMetadata(StreamEventKeys.UserSentMessage, "Changed description");

        registry.Register(first);
        registry.Register(second);

        registry.GetAll().Should().ContainSingle()
            .Which.Should().Be(first);
    }

    [Fact]
    public void Given_SystemEvent_When_Registered_Then_ItIsKnownButHiddenFromWorkflowQueries()
    {
        var registry = new InMemoryStreamEventTypeRegistry();

        registry.Register(new StreamEventTypeMetadata(
            StreamEventKeys.PlatformConnectionChanged,
            "Platform connection changed",
            IsSystemEvent: true));

        registry.IsKnown(StreamEventKeys.PlatformConnectionChanged).Should().BeTrue();
        registry.IsKnownForWorkflow(StreamEventKeys.PlatformConnectionChanged).Should().BeFalse();
        registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Given_WorkflowVisibleEvent_When_Registered_Then_ItIsKnownForWorkflowAndReturnedByGetAll()
    {
        var registry = new InMemoryStreamEventTypeRegistry();
        var metadata = new StreamEventTypeMetadata(StreamEventKeys.UserFollowed, "User followed");

        registry.Register(metadata);

        registry.IsKnown(StreamEventKeys.UserFollowed).Should().BeTrue();
        registry.IsKnownForWorkflow(StreamEventKeys.UserFollowed).Should().BeTrue();
        registry.GetAll().Should().ContainSingle()
            .Which.Should().Be(metadata);
    }
}
