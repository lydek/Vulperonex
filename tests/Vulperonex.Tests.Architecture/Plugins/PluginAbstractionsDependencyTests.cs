using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Plugins;

public sealed class PluginAbstractionsDependencyTests
{
    [Fact]
    public void Given_PluginAbstractionsAssembly_When_Inspected_Then_ItDoesNotDependOnInfrastructureHostsOrAdapters()
    {
        string[] forbiddenDependencies =
        [
            "Vulperonex.Infrastructure",
            "Vulperonex.Web",
            "Vulperonex.Cli",
            "Vulperonex.Desktop",
            "Vulperonex.Adapters",
        ];

        foreach (var dependency in forbiddenDependencies)
        {
            Types.InAssembly(typeof(Vulperonex.Plugins.Abstractions.AssemblyMarker).Assembly)
                .Should()
                .NotHaveDependencyOn(dependency)
                .GetResult()
                .IsSuccessful
                .Should()
                .BeTrue($"Plugins.Abstractions must not depend on {dependency}");
        }
    }
}
