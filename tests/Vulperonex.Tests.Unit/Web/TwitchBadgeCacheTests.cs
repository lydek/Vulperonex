using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vulperonex.Application.Twitch;
using Vulperonex.Adapters.Twitch.Helix;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class TwitchBadgeCacheTests
{
    [Fact]
    public async Task SyncGlobalAsync_PopulatesCacheAndExposesUrlsByKey()
    {
        var (cache, _) = CreateCacheWith(global: new[]
        {
            Badge("vip_1", "vip", "1", "https://cdn/vip.png", "VIP"),
            Badge("subscriber_0", "subscriber", "0", "https://cdn/sub.png", "Subscriber"),
        });

        await cache.SyncGlobalAsync();

        cache.IsReady.Should().BeTrue();
        cache.GetUrl("vip_1").Should().Be("https://cdn/vip.png");
        cache.GetUrl("vip/1").Should().Be("https://cdn/vip.png");
        cache.GetUrl("subscriber/0").Should().Be("https://cdn/sub.png");
        cache.ListGlobal().Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncChannelAsync_ChannelBadgesShadowGlobalForSameKey()
    {
        var (cache, _) = CreateCacheWith(
            global: new[] { Badge("subscriber_0", "subscriber", "0", "https://cdn/global-sub.png", "Subscriber") },
            channel: new[] { Badge("subscriber_0", "subscriber", "0", "https://cdn/channel-sub.png", "Channel Sub", isChannel: true) });

        await cache.SyncGlobalAsync();
        await cache.SyncChannelAsync("broadcaster-1");

        cache.GetUrl("subscriber_0").Should().Be("https://cdn/channel-sub.png");
        cache.ListChannel().Should().ContainSingle();
    }

    [Fact]
    public void GetUrl_ReturnsNullForUnknownKey()
    {
        var (cache, _) = CreateCacheWith(global: System.Array.Empty<PlatformBadgeDescriptor>());

        cache.GetUrl("missing_99").Should().BeNull();
        cache.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task SyncGlobalAsync_SwallowsHelixFailureWithoutMarkingReady()
    {
        var client = Substitute.For<IHelixClient>();
        client.GetGlobalBadgesAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PlatformBadgeDescriptor>>(_ => throw new System.Net.Http.HttpRequestException("boom"));

        var cache = BuildCacheWithClient(client);

        await cache.SyncGlobalAsync();

        cache.IsReady.Should().BeFalse();
        cache.ListGlobal().Should().BeEmpty();
    }

    private static PlatformBadgeDescriptor Badge(
        string key, string setId, string version, string url, string title, bool isChannel = false)
    {
        return new PlatformBadgeDescriptor(key, setId, version, url, title, Description: null, IsChannel: isChannel);
    }

    private static (TwitchBadgeCache Cache, IHelixClient Client) CreateCacheWith(
        IReadOnlyList<PlatformBadgeDescriptor> global,
        IReadOnlyList<PlatformBadgeDescriptor>? channel = null)
    {
        var client = Substitute.For<IHelixClient>();
        client.GetGlobalBadgesAsync(Arg.Any<CancellationToken>()).Returns(global);
        client.GetChannelBadgesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(channel ?? new List<PlatformBadgeDescriptor>());

        return (BuildCacheWithClient(client), client);
    }

    private static TwitchBadgeCache BuildCacheWithClient(IHelixClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);
        var provider = services.BuildServiceProvider();
        return new TwitchBadgeCache(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<TwitchBadgeCache>.Instance);
    }
}
