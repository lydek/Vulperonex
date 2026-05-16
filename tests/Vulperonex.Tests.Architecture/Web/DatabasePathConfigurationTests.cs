using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Architecture.Web;

public sealed class DatabasePathConfigurationTests
{
    [Fact]
    public void Given_WebSource_When_DatabasePathIsRead_Then_OnlyResolverReadsRawConfigurationKey()
    {
        var matches = Directory
            .EnumerateFiles(GetWebSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("Database:Path", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(GetRepositoryRoot(), path).Replace('\\', '/'))
            .ToArray();

        matches.Should().BeEquivalentTo(
            "src/Hosts/Vulperonex.Web/Configuration/DatabasePathResolver.cs");
    }

    private static string GetWebSourceRoot()
    {
        return Path.Combine(GetRepositoryRoot(), "src", "Hosts", "Vulperonex.Web");
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
}
