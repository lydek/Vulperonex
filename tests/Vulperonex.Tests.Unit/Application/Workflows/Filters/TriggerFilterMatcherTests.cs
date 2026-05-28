using FluentAssertions;
using Vulperonex.Application.Workflows.Filters;
using Vulperonex.Application.Workflows.Filters.Matchers;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Workflows.Filters;

/// <summary>
/// Phase 8 / Phase C: edge cases for per-event-type filter matchers.
/// Covers boundary checks the generic dict dispatch never enforced (e.g. `!so`
/// must not match `!sorry`), and threshold comparisons (`MinAmount`,
/// `MinViewers`, `MinGiftCount`).
/// </summary>
public sealed class TriggerFilterMatcherTests
{
    // ---------------------------------------------------------------------
    // MatchChatMessage — boundary check
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("!so", "!so", true)]
    [InlineData("!so", "!so target", true)]
    [InlineData("!so", "!sorry", false)]
    [InlineData("!so", "!sorry I missed it", false)]
    [InlineData("!so", "!Solid", false)]
    [InlineData("!so", "prefix !so", false)]
    [InlineData("!SO", "!so", true)] // case-insensitive
    [InlineData("!so", "!SO", true)]
    public void MatchChatMessage_CommandName_EnforcesWordBoundary(string commandName, string message, bool expected)
    {
        var matcher = new MatchChatMessage();
        var filter = new Dictionary<string, string> { ["CommandName"] = commandName };
        var triggerValues = new Dictionary<string, object?> { ["MessageText"] = message };

        matcher.Match(filter, triggerValues).Should().Be(expected);
    }

    [Theory]
    [InlineData("!", "!checkin", true)]
    [InlineData("!", "hello", false)]
    [InlineData("?", "?ask", true)]
    public void MatchChatMessage_Prefix_MatchesStartOnly(string prefix, string message, bool expected)
    {
        var matcher = new MatchChatMessage();
        var filter = new Dictionary<string, string> { ["Prefix"] = prefix };
        var triggerValues = new Dictionary<string, object?> { ["MessageText"] = message };

        matcher.Match(filter, triggerValues).Should().Be(expected);
    }

    [Fact]
    public void MatchChatMessage_WithNoMessageText_ReturnsFalse()
    {
        var matcher = new MatchChatMessage();
        matcher.Match(
            new Dictionary<string, string> { ["CommandName"] = "!so" },
            new Dictionary<string, object?>()).Should().BeFalse();
    }

    [Fact]
    public void MatchChatMessage_EmptyFilter_MatchesAnyMessage()
    {
        var matcher = new MatchChatMessage();
        matcher.Match(
            new Dictionary<string, string>(),
            new Dictionary<string, object?> { ["MessageText"] = "hello" }).Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // MatchUserDonated — MinAmount threshold
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("100", 99, false)]
    [InlineData("100", 100, true)]
    [InlineData("100", 101, true)]
    [InlineData("100", 50, false)] // Plan acceptance: MinAmount=100 不匹 Bits=50
    public void MatchUserDonated_MinAmount_GreaterOrEqual(string minAmount, decimal actual, bool expected)
    {
        var matcher = new MatchUserDonated();
        matcher.Match(
            new Dictionary<string, string> { ["MinAmount"] = minAmount },
            new Dictionary<string, object?> { ["Amount"] = actual }).Should().Be(expected);
    }

    [Fact]
    public void MatchUserDonated_MissingAmountInTrigger_ReturnsFalse()
    {
        var matcher = new MatchUserDonated();
        matcher.Match(
            new Dictionary<string, string> { ["MinAmount"] = "10" },
            new Dictionary<string, object?>()).Should().BeFalse();
    }

    [Fact]
    public void MatchUserDonated_NoFilter_MatchesAnyAmount()
    {
        var matcher = new MatchUserDonated();
        matcher.Match(
            new Dictionary<string, string>(),
            new Dictionary<string, object?> { ["Amount"] = 5m }).Should().BeTrue();
    }

    // ---------------------------------------------------------------------
    // Registry routing
    // ---------------------------------------------------------------------

    [Fact]
    public void Registry_DispatchesByEventTypeKey()
    {
        var registry = new TriggerFilterMatcherRegistry(
            new ITriggerFilterMatcher[] { new MatchChatMessage(), new MatchUserDonated() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TriggerFilterMatcherRegistry>.Instance);

        registry.TryMatch(
            "user.message",
            new Dictionary<string, string> { ["CommandName"] = "!so" },
            new Dictionary<string, object?> { ["MessageText"] = "!sorry" },
            out var isMatch).Should().BeTrue("user.message matcher must be registered");
        isMatch.Should().BeFalse("!sorry must not match !so");
    }

    [Fact]
    public void Registry_UnknownEventType_ReturnsFalseTryMatch()
    {
        var registry = new TriggerFilterMatcherRegistry(
            Array.Empty<ITriggerFilterMatcher>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TriggerFilterMatcherRegistry>.Instance);

        registry.TryMatch(
            "unknown.event",
            new Dictionary<string, string>(),
            new Dictionary<string, object?>(),
            out _).Should().BeFalse("no matcher registered => caller must fall back");
    }

    [Fact]
    public void Registry_CaseInsensitiveEventTypeLookup()
    {
        var registry = new TriggerFilterMatcherRegistry(
            new ITriggerFilterMatcher[] { new MatchChatMessage() },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TriggerFilterMatcherRegistry>.Instance);

        registry.TryMatch(
            "USER.MESSAGE",
            new Dictionary<string, string>(),
            new Dictionary<string, object?> { ["MessageText"] = "x" },
            out _).Should().BeTrue();
    }
}
