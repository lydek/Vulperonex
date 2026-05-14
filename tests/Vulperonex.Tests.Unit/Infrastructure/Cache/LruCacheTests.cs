using FluentAssertions;
using Vulperonex.Infrastructure.Cache;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.Cache;

public sealed class LruCacheTests
{
    [Fact]
    public void Given_CapacityExceeded_When_AddingItems_Then_LeastRecentlyUsedItemIsRemoved()
    {
        var cache = new LruCache<string, int>(capacity: 2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _);
        cache.Set("c", 3);

        cache.TryGet("a", out _).Should().BeTrue();
        cache.TryGet("b", out _).Should().BeFalse();
        cache.TryGet("c", out _).Should().BeTrue();
    }
}
