using FluentAssertions;
using Vulperonex.Application.EventTypes;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.EventTypes;

public sealed class StreamEventTypeRegistryContractTests
{
    [Fact]
    public void Given_StreamEventTypeRegistryContract_When_Inspected_Then_ItExposesRegistrationAndQueryMethods()
    {
        var methodNames = typeof(IStreamEventTypeRegistry)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        methodNames.Should().BeEquivalentTo("Register", "IsKnown", "IsKnownForWorkflow", "GetAll");
    }

    [Fact]
    public void Given_StreamEventTypeMetadata_When_Created_Then_ItContainsWorkflowVisibilityFields()
    {
        var metadata = new StreamEventTypeMetadata("user.message", "User sent a message", IsSystemEvent: false);

        metadata.Key.Should().Be("user.message");
        metadata.Description.Should().Be("User sent a message");
        metadata.IsSystemEvent.Should().BeFalse();
    }
}
