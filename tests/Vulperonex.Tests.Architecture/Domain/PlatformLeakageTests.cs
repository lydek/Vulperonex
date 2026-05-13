using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Domain;

public sealed class PlatformLeakageTests
{
    [Fact]
    public void Given_DomainAndApplicationAssemblies_When_TypesAreInspected_Then_PlatformSpecificTypeNamesAreAbsent()
    {
        var assemblies = new[]
        {
            typeof(Vulperonex.Domain.AssemblyMarker).Assembly,
            typeof(Vulperonex.Application.AssemblyMarker).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            var platformSpecificTypes = assembly
                .GetTypes()
                .Where(type => type.Name.StartsWith("Twitch", StringComparison.Ordinal))
                .Select(type => type.FullName)
                .ToArray();

            platformSpecificTypes.Should().BeEmpty($"{assembly.GetName().Name} must not contain Twitch-specific type names");
        }
    }

    [Fact]
    public void Given_DomainAssembly_When_DependenciesAreInspected_Then_DomainDoesNotReferenceAdapterAssemblies()
    {
        Types.InAssembly(typeof(Vulperonex.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("Vulperonex.Adapters")
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue("Domain must not reference adapter assemblies");
    }

    [Fact]
    public void Given_ApplicationAssembly_When_DependenciesAreInspected_Then_ApplicationDoesNotReferenceAdapterAssemblies()
    {
        Types.InAssembly(typeof(Vulperonex.Application.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("Vulperonex.Adapters")
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue("Application must not reference adapter assemblies");
    }
}
