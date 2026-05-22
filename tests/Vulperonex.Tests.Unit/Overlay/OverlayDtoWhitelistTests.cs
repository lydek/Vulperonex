using System.Text.Json;
using FluentAssertions;
using Vulperonex.Application.Overlay.Dtos;
using Xunit;

namespace Vulperonex.Tests.Unit.Overlay;

public sealed class OverlayDtoWhitelistTests
{
    [Fact]
    public void Given_OverlayChatPayload_When_Serialized_Then_JsonKeySetIsExact()
    {
        var payload = new OverlayChatPayload(1, "evt-1", DateTimeOffset.UnixEpoch, "Alice", "#ffffff", [new("text", "hello")], ["subscriber/1"]);

        SerializeKeys(payload).Should().BeEquivalentTo("schemaVersion", "eventId", "timestamp", "displayName", "colorHex", "segments", "badges");
    }

    [Fact]
    public void Given_OverlayAlertPayload_When_Serialized_Then_JsonKeySetIsExact()
    {
        var payload = new OverlayAlertPayload(1, "evt-1", DateTimeOffset.UnixEpoch, "Alice", "subscription", "1000");

        SerializeKeys(payload).Should().BeEquivalentTo("schemaVersion", "eventId", "timestamp", "displayName", "eventType", "tier", "replayed");
    }

    [Fact]
    public void Given_OverlayMemberPayload_When_Serialized_Then_JsonKeySetIsExact()
    {
        var payload = new OverlayMemberPayload(1, "Alice", "avatar", 3);

        SerializeKeys(payload).Should().BeEquivalentTo("schemaVersion", "displayName", "avatarUrl", "checkInCount");
    }

    [Fact]
    public void Given_OverlayEffectPayload_When_Serialized_Then_JsonKeySetIsExact()
    {
        var payload = new OverlayEffectPayload(1, "evt-1", DateTimeOffset.UnixEpoch, "sparkle", 1_000);

        SerializeKeys(payload).Should().BeEquivalentTo("schemaVersion", "eventId", "timestamp", "effectId", "durationMs");
    }

    [Fact]
    public void Given_OverlayTextSegmentTypeIsNotAllowed_When_Created_Then_ItIsRejected()
    {
        var create = () => new OverlayTextSegment("script", "alert(1)");

        create.Should().Throw<ArgumentException>();
    }

    private static string[] SerializeKeys<T>(T payload)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOptions));
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
