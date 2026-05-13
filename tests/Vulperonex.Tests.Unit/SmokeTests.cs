using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Unit;

public sealed class SmokeTests
{
    [Fact]
    public void Given_UnitTestProject_When_Discovered_Then_SmokeTestPasses()
    {
        true.Should().BeTrue();
    }
}
