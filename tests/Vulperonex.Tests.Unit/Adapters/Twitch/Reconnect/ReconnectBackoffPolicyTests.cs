using FluentAssertions;
using Vulperonex.Adapters.Twitch.Reconnect;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.Reconnect;

public sealed class ReconnectBackoffPolicyTests
{
    [Fact]
    public void Given_ReconnectAttempts_When_DelayCalculated_Then_ItUsesExponentialBackoffCappedAtSixtySeconds()
    {
        var policy = new ReconnectBackoffPolicy();

        policy.GetDelay(1).Should().Be(TimeSpan.FromSeconds(1));
        policy.GetDelay(2).Should().Be(TimeSpan.FromSeconds(2));
        policy.GetDelay(3).Should().Be(TimeSpan.FromSeconds(4));
        policy.GetDelay(10).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Given_Jitter_When_DelayCalculated_Then_ItIsClampedWithinTwentyPercent()
    {
        new ReconnectBackoffPolicy(0.5).GetDelay(2).Should().Be(TimeSpan.FromSeconds(2.4));
        new ReconnectBackoffPolicy(-0.5).GetDelay(2).Should().Be(TimeSpan.FromSeconds(1.6));
    }
}
