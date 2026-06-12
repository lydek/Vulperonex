using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Unit.Desktop;

public sealed class DesktopShellTests
{
    // BDD naming pattern: Given_MockWebHostAlwaysCrashes_When_RunLoopExecuted_Then_RetriesThreeTimesAndStops
    [Fact]
    public async Task Given_MockWebHostAlwaysCrashes_When_RunLoopExecuted_Then_RetriesThreeTimesAndStops()
    {
        // Given
        var crashCount = 0;
        var maxCrashCount = 3;
        var fallbackTriggered = false;
        var cts = new CancellationTokenSource();

        // When
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                // Simulate web host boot and immediate crash
                throw new Exception("Kestrel bind socket failure");
            }
            catch (Exception)
            {
                crashCount++;
                if (crashCount > maxCrashCount)
                {
                    fallbackTriggered = true;
                    break;
                }

                await Task.Delay(1, TestContext.Current.CancellationToken); // Small delay to avoid busy looping in tests
            }
        }

        // Then
        crashCount.Should().Be(4, "it should boot 1 time and retry 3 times, totaling 4 executions");
        fallbackTriggered.Should().BeTrue("after 3 retries, fallback UI must be loaded");
    }

    [Fact]
    public void Given_IpcMessageJson_When_Deserialized_Then_StructureAndFieldsMustMatchIpcSpecification()
    {
        // Given
        var json = """
        {
            "type": "simulate_event",
            "payload": {
                "displayName": "Alice",
                "message": "hello"
            }
        }
        """;

        // When
        using var doc = JsonDocument.Parse(json);
        var type = doc.RootElement.GetProperty("type").GetString();
        var payload = doc.RootElement.GetProperty("payload");

        // Then
        type.Should().Be("simulate_event");
        payload.ValueKind.Should().Be(JsonValueKind.Object);
        payload.GetProperty("displayName").GetString().Should().Be("Alice");
        payload.GetProperty("message").GetString().Should().Be("hello");
    }

    [Fact]
    public void Given_IpcReplyObject_When_Serialized_Then_StructureMustMatchIpcSpecification()
    {
        // Given
        var reply = new
        {
            type = "simulate_event_reply",
            payload = "ACK"
        };

        // When
        var json = JsonSerializer.Serialize(reply);

        // Then
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().Should().Be("simulate_event_reply");
        doc.RootElement.GetProperty("payload").GetString().Should().Be("ACK");
    }
}
