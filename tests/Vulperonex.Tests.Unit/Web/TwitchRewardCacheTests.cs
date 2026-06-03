using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vulperonex.Application.Settings;
using Vulperonex.Application.Twitch;
using Vulperonex.Web.TwitchAuth;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class TwitchRewardCacheTests
{
    [Fact]
    public async Task RefreshAsync_PopulatesCacheFromHelix()
    {
        var helix = Substitute.For<IHelixClient>();
        helix.GetCustomRewardsAsync("broadcaster-1", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new PlatformRewardDescriptor("r-1", "Hydrate", 100, true, null),
                new PlatformRewardDescriptor("r-2", "Stretch", 200, true, null),
            });

        var cache = BuildCache(helix, configuredBroadcasterId: "broadcaster-1");

        cache.IsReady.Should().BeFalse();
        cache.List().Should().BeEmpty();

        await cache.RefreshAsync(TestContext.Current.CancellationToken);

        cache.IsReady.Should().BeTrue();
        cache.LastRefreshedAt.Should().NotBeNull();
        cache.List().Select(r => r.Title).Should().Equal("Hydrate", "Stretch");
    }

    [Fact]
    public async Task RefreshAsync_ResolvesBroadcasterFromChannelNameSetting()
    {
        var helix = Substitute.For<IHelixClient>();
        helix.LookupUserAsync("mychannel", null, Arg.Any<CancellationToken>())
            .Returns(new PlatformUserProfile("broadcaster-77", "mychannel", "MyChannel", null, null, false));
        helix.GetCustomRewardsAsync("broadcaster-77", Arg.Any<CancellationToken>())
            .Returns(new[] { new PlatformRewardDescriptor("r-1", "Hydrate", 100, true, null) });

        var settings = Substitute.For<ISystemSettingsService>();
        settings.GetAsync<string?>(SystemSettingKey.TwitchChannelName, null, Arg.Any<CancellationToken>())
            .Returns("mychannel");

        var cache = BuildCache(helix, settings: settings);

        await cache.RefreshAsync(TestContext.Current.CancellationToken);

        cache.IsReady.Should().BeTrue();
        cache.List().Should().ContainSingle().Subject.Title.Should().Be("Hydrate");
    }

    [Fact]
    public async Task RefreshAsync_SkipsWhenBroadcasterUnresolved()
    {
        var helix = Substitute.For<IHelixClient>();
        var cache = BuildCache(helix);

        await cache.RefreshAsync(TestContext.Current.CancellationToken);

        cache.IsReady.Should().BeFalse();
        await helix.DidNotReceive().GetCustomRewardsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReplacesSnapshotOnSubsequentCall()
    {
        var helix = Substitute.For<IHelixClient>();
        helix.GetCustomRewardsAsync("broadcaster-1", Arg.Any<CancellationToken>())
            .Returns(
                new[] { new PlatformRewardDescriptor("r-1", "Hydrate", 100, true, null) },
                new[]
                {
                    new PlatformRewardDescriptor("r-1", "Hydrate", 100, true, null),
                    new PlatformRewardDescriptor("r-2", "Stretch", 200, true, null),
                });

        var cache = BuildCache(helix, configuredBroadcasterId: "broadcaster-1");

        await cache.RefreshAsync(TestContext.Current.CancellationToken);
        cache.List().Should().ContainSingle();

        await cache.RefreshAsync(TestContext.Current.CancellationToken);
        cache.List().Should().HaveCount(2);
    }

    private static TwitchRewardCache BuildCache(
        IHelixClient helix,
        ISystemSettingsService? settings = null,
        string? configuredBroadcasterId = null,
        string? configuredChannelName = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(helix);
        services.AddSingleton(settings ?? Substitute.For<ISystemSettingsService>());
        var provider = services.BuildServiceProvider();

        var configValues = new Dictionary<string, string?>();
        if (configuredBroadcasterId is not null)
        {
            configValues["Twitch:BroadcasterId"] = configuredBroadcasterId;
        }
        if (configuredChannelName is not null)
        {
            configValues["Twitch:ChannelName"] = configuredChannelName;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var lifetime = Substitute.For<IHostApplicationLifetime>();

        return new TwitchRewardCache(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            lifetime,
            NullLogger<TwitchRewardCache>.Instance);
    }
}
