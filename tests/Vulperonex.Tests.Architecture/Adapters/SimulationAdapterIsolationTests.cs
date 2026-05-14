using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Adapters;

public sealed class SimulationAdapterIsolationTests
{
    [Fact]
    public void Given_SimulationAdapterAssembly_When_DependenciesAreInspected_Then_ItDoesNotReferenceTwitchAdapter()
    {
        Types.InAssembly(typeof(Vulperonex.Adapters.Simulation.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("Vulperonex.Adapters.Twitch")
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue("Simulation adapter must not reference Twitch adapter types");
    }

    [Fact]
    public void Given_SimulationAdapterAssembly_When_TypesAreInspected_Then_NoTwitchSpecificTypesAreUsed()
    {
        var twitchNamedTypes = typeof(Vulperonex.Adapters.Simulation.AssemblyMarker).Assembly
            .GetTypes()
            .Where(type => type.FullName?.Contains("Twitch", StringComparison.Ordinal) is true)
            .Select(type => type.FullName)
            .ToArray();

        twitchNamedTypes.Should().BeEmpty("Simulation adapter must stay platform-neutral");
    }
}
