using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Architecture;

public sealed class SmokeTests
{
    [Fact]
    public void Given_ArchitectureTestProject_When_Discovered_Then_SmokeTestPasses()
    {
        true.Should().BeTrue();
    }
}
