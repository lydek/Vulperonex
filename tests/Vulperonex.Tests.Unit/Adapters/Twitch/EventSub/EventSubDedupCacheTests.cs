using FluentAssertions;
using Vulperonex.Adapters.Twitch.EventSub;
using Xunit;

namespace Vulperonex.Tests.Unit.Adapters.Twitch.EventSub;

public sealed class EventSubDedupCacheTests
{
    [Fact]
    public void Given_DuplicateSourceEventId_When_Marked_Then_OnlyFirstDeliveryIsNew()
    {
        var cache = new EventSubDedupCache(TimeProvider.System);

        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
        cache.TryMarkNew("twitch", "evt-1").Should().BeFalse();
    }
}
