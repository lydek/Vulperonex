using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Integration;

public sealed class SmokeTests
{
    [Fact]
    public void Given_IntegrationTestProject_When_Discovered_Then_SmokeTestPasses()
    {
        true.Should().BeTrue();
    }
}
