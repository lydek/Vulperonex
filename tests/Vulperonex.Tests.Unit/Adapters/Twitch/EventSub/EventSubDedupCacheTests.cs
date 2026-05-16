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

    [Fact]
    public void Given_SourceEventIdExpires_When_MarkedAgain_Then_DeliveryIsNew()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero));
        var cache = new EventSubDedupCache(timeProvider);

        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
        timeProvider.Advance(TimeSpan.FromMinutes(11));

        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
    }

    [Fact]
    public void Given_CapacityIsExceeded_When_OldestEntryIsMarkedAgain_Then_ItIsAccepted()
    {
        var cache = new EventSubDedupCache(TimeProvider.System, capacity: 2);

        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
        cache.TryMarkNew("twitch", "evt-2").Should().BeTrue();
        cache.TryMarkNew("twitch", "evt-3").Should().BeTrue();

        cache.TryMarkNew("twitch", "evt-3").Should().BeFalse();
        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
    }

    [Fact]
    public void Given_SameSourceEventIdOnDifferentPlatform_When_Marked_Then_BothAreAccepted()
    {
        var cache = new EventSubDedupCache(TimeProvider.System);

        cache.TryMarkNew("twitch", "evt-1").Should().BeTrue();
        cache.TryMarkNew("youtube", "evt-1").Should().BeTrue();
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow += delta;
        }
    }
}
