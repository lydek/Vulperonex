using System.Linq;
using FluentAssertions;
using NetArchTest.Rules;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Domain.Events;
using Xunit;

namespace Vulperonex.Tests.Architecture;

/// <summary>
/// Enforces SPEC §6.1 "Key Naming Rules": the Domain and Application projects
/// must not contain platform-specific type-name prefixes (e.g. <c>Twitch*</c>).
/// Platform vocabularies are strictly restricted to the corresponding
/// <c>Adapters.{Platform}</c> projects.
/// </summary>
public sealed class PlatformPrefixIsolationTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Twitch",
        "YouTube",
        "Kick",
        "OneComme",
    ];

    [Fact]
    public void Domain_types_must_not_start_with_a_platform_prefix()
    {
        var assembly = typeof(IStreamEvent).Assembly;

        var offenders = Types.InAssembly(assembly)
            .That()
            .DoNotHaveCustomAttribute(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
            .GetTypes()
            .Where(t => !string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Vulperonex.Domain"))
            .Where(t => ForbiddenPrefixes.Any(prefix => t.Name.StartsWith(prefix, System.StringComparison.Ordinal)))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .ToArray();

        offenders.Should().BeEmpty(
            "Domain types must stay platform-agnostic (SPEC §6.1). Offenders: {0}",
            string.Join(", ", offenders));
    }

    [Fact]
    public void Application_types_must_not_start_with_a_platform_prefix()
    {
        var assembly = typeof(WorkflowAction).Assembly;

        var offenders = Types.InAssembly(assembly)
            .That()
            .DoNotHaveCustomAttribute(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
            .GetTypes()
            .Where(t => !string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith("Vulperonex.Application"))
            // Vulperonex.Application.Twitch is intentionally the platform-neutral
            // Helix client abstraction namespace: its members (IHelixClient,
            // PlatformUserProfile, etc.) are platform-agnostic by name. The
            // namespace itself is allowed; only platform-prefixed *type names*
            // are forbidden.
            .Where(t => ForbiddenPrefixes.Any(prefix => t.Name.StartsWith(prefix, System.StringComparison.Ordinal)))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .ToArray();

        offenders.Should().BeEmpty(
            "Application types must stay platform-agnostic (SPEC §6.1). " +
            "Move platform-specific types to Vulperonex.Adapters.{Platform}, or rename them platform-neutral. " +
            "Offenders: {0}",
            string.Join(", ", offenders));
    }
}
