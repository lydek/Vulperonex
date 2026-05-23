using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Vulperonex.Application.Overlay.Dtos;
using Xunit;

namespace Vulperonex.Tests.Architecture.Overlay;

public sealed class ChatHtmlPayloadKeysTest
{
    [Fact]
    public void Given_ChatJsFile_When_Inspected_Then_AllDataPropertiesAreSubsetOfOverlayChatPayload()
    {
        // 1. Resolve chat.js path robustly by traversing up to solution root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vulperonex.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("Solution root directory must be found");
        var chatJsPath = Path.Combine(dir!.FullName, "src", "Hosts", "Vulperonex.Web", "wwwroot", "overlay", "js", "chat.js");
        File.Exists(chatJsPath).Should().BeTrue("chat.js must exist at {0}", chatJsPath);

        var chatJsContent = File.ReadAllText(chatJsPath!);

        // 2. Parse references like data.foo or data.fooBar
        var matches = Regex.Matches(chatJsContent, @"data\.([a-zA-Z0-9_]+)");
        var referencedKeys = matches
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        referencedKeys.Should().NotBeEmpty("chat.js should reference at least some properties of data");

        // 3. Get all property names of OverlayChatPayload in lowercase
        var lowercaseProperties = typeof(OverlayChatPayload)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        // 4. Assert that referencedKeys is subset of payloadProperties (case-insensitive)
        foreach (var key in referencedKeys)
        {
            lowercaseProperties.Should().Contain(
                key.ToLowerInvariant(),
                $"chat.js property '{key}' must be defined in {nameof(OverlayChatPayload)}");
        }
    }
}
