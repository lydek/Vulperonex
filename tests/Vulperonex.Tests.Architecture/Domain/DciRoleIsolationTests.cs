using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Domain;

public sealed class DciRoleIsolationTests
{
    private static readonly string[] ForbiddenDependencies =
    [
        "Vulperonex.Infrastructure",
        "Microsoft.EntityFrameworkCore",
    ];

    [Fact]
    public void Given_DomainRoleTypes_When_DependenciesAreInspected_Then_RolesDoNotReferenceInfrastructure()
    {
        var roleTypes = Types.InAssembly(typeof(Vulperonex.Domain.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Role")
            .GetTypes()
            .ToArray();

        AssertTypesHaveNoForbiddenDependencies(roleTypes, "Role");
    }

    [Fact]
    public void Given_DomainBehaviorTypes_When_DependenciesAreInspected_Then_BehaviorsDoNotReferenceInfrastructure()
    {
        var behaviorTypes = Types.InAssembly(typeof(Vulperonex.Domain.AssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Behavior")
            .GetTypes()
            .ToArray();

        AssertTypesHaveNoForbiddenDependencies(behaviorTypes, "Behavior");
    }

    private static void AssertTypesHaveNoForbiddenDependencies(IReadOnlyCollection<Type> types, string suffix)
    {
        if (types.Count == 0)
        {
            return;
        }

        foreach (var dependency in ForbiddenDependencies)
        {
            var result = Types.InAssembly(typeof(Vulperonex.Domain.AssemblyMarker).Assembly)
                .That()
                .HaveNameEndingWith(suffix)
                .Should()
                .NotHaveDependencyOn(dependency)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Domain {suffix} types must not depend on {dependency}");
        }
    }
}
