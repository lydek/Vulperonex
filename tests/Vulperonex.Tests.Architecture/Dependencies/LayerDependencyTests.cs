using System.Xml.Linq;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Vulperonex.Tests.Architecture.Dependencies;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Given_DomainProject_When_ProjectReferencesAreInspected_Then_DomainHasNoProjectReferences()
    {
        GetProjectReferences("src/Vulperonex.Domain/Vulperonex.Domain.csproj")
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void Given_ApplicationProject_When_ProjectReferencesAreInspected_Then_ApplicationReferencesDomainOnly()
    {
        GetProjectReferences("src/Vulperonex.Application/Vulperonex.Application.csproj")
            .Should()
            .BeEquivalentTo("src/Vulperonex.Domain/Vulperonex.Domain.csproj");
    }

    [Fact]
    public void Given_InfrastructureProject_When_ProjectReferencesAreInspected_Then_InfrastructureReferencesApplicationAndDomain()
    {
        GetProjectReferences("src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj")
            .Should()
            .BeEquivalentTo(
                "src/Vulperonex.Application/Vulperonex.Application.csproj",
                "src/Vulperonex.Domain/Vulperonex.Domain.csproj");
    }

    [Fact]
    public void Given_AdapterProjects_When_ProjectReferencesAreInspected_Then_AdaptersFollowTheApprovedGraph()
    {
        GetProjectReferences("src/Adapters/Vulperonex.Adapters.Abstractions/Vulperonex.Adapters.Abstractions.csproj")
            .Should()
            .BeEmpty();

        string[] adapterReferences =
        [
            "src/Vulperonex.Application/Vulperonex.Application.csproj",
            "src/Vulperonex.Domain/Vulperonex.Domain.csproj",
            "src/Adapters/Vulperonex.Adapters.Abstractions/Vulperonex.Adapters.Abstractions.csproj",
        ];

        GetProjectReferences("src/Adapters/Vulperonex.Adapters.Simulation/Vulperonex.Adapters.Simulation.csproj")
            .Should()
            .BeEquivalentTo(adapterReferences);

        GetProjectReferences("src/Adapters/Vulperonex.Adapters.Twitch/Vulperonex.Adapters.Twitch.csproj")
            .Should()
            .BeEquivalentTo(adapterReferences);
    }

    [Fact]
    public void Given_HostProjects_When_ProjectReferencesAreInspected_Then_HostsFollowTheApprovedGraph()
    {
        string[] hostReferences =
        [
            "src/Adapters/Vulperonex.Adapters.Abstractions/Vulperonex.Adapters.Abstractions.csproj",
            "src/Adapters/Vulperonex.Adapters.Simulation/Vulperonex.Adapters.Simulation.csproj",
            "src/Adapters/Vulperonex.Adapters.Twitch/Vulperonex.Adapters.Twitch.csproj",
            "src/Vulperonex.Application/Vulperonex.Application.csproj",
            "src/Vulperonex.Domain/Vulperonex.Domain.csproj",
            "src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj",
            "src/Vulperonex.Plugins.Abstractions/Vulperonex.Plugins.Abstractions.csproj",
        ];

        GetProjectReferences("src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj")
            .Should()
            .BeEquivalentTo(hostReferences);

        GetProjectReferences("src/Hosts/Vulperonex.Cli/Vulperonex.Cli.csproj")
            .Should()
            .BeEquivalentTo(hostReferences);

        GetProjectReferences("src/Hosts/Vulperonex.Desktop/Vulperonex.Desktop.csproj")
            .Should()
            .BeEquivalentTo("src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj");
    }

    [Fact]
    public void Given_DomainAssembly_When_Inspected_Then_DomainDoesNotDependOnInfrastructureOrAdapters()
    {
        string[] forbiddenDependencies =
        [
            "Vulperonex.Infrastructure",
            "Vulperonex.Application",
            "Vulperonex.Adapters",
            "Vulperonex.Plugins",
            "Vulperonex.Web",
            "Vulperonex.Cli",
            "Vulperonex.Desktop",
        ];

        foreach (var dependency in forbiddenDependencies)
        {
            Types.InAssembly(typeof(Vulperonex.Domain.AssemblyMarker).Assembly)
                .Should()
                .NotHaveDependencyOn(dependency)
                .GetResult()
                .IsSuccessful
                .Should()
                .BeTrue($"Domain must not depend on {dependency}");
        }
    }

    private static string[] GetProjectReferences(string projectPath)
    {
        var projectFullPath = Path.Combine(GetRepositoryRoot(), NormalizePath(projectPath));
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");

        return XDocument.Load(projectFullPath)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!)))
            .Select(path => Path.GetRelativePath(GetRepositoryRoot(), path))
            .Select(NormalizePath)
            .Order()
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vulperonex.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
