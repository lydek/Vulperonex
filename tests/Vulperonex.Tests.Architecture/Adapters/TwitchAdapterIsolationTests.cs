using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Adapters;

public sealed class TwitchAdapterIsolationTests
{
    [Fact]
    public void Given_DomainAndApplicationAssemblies_When_DependenciesAreInspected_Then_TheyDoNotReferenceTwitchAdapter()
    {
        Types.InAssemblies(
            [
                typeof(Vulperonex.Domain.AssemblyMarker).Assembly,
                typeof(Vulperonex.Application.AssemblyMarker).Assembly,
            ])
            .Should()
            .NotHaveDependencyOn("Vulperonex.Adapters.Twitch")
            .GetResult()
            .IsSuccessful
            .Should()
            .BeTrue("Domain and Application must not depend on Twitch adapter types");
    }
}
