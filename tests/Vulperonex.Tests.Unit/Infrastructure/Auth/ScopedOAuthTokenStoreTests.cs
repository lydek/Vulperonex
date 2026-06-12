using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Application.Auth;
using Vulperonex.Infrastructure.Auth;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.Auth;

/// <summary>
/// The scope-bridging store lets the singleton TwitchAccessTokenProvider talk
/// to the EF-backed scoped token store. Each call must open a fresh scope —
/// reusing one would capture a long-lived DbContext inside a singleton.
/// </summary>
public sealed class ScopedOAuthTokenStoreTests
{
    [Fact]
    public async Task Given_ScopedInnerStore_When_BridgeCalled_Then_DelegatesAllOperations()
    {
        var inner = new RecordingInnerStore();
        await using var provider = BuildProvider(inner);
        var bridge = new ScopedOAuthTokenStore(provider.GetRequiredService<IServiceScopeFactory>());

        await bridge.StoreRefreshTokenAsync("twitch", "token-1", TestContext.Current.CancellationToken);
        var fetched = await bridge.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);
        var has = await bridge.HasRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);
        await bridge.ClearRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);

        fetched.Should().Be("token-1");
        has.Should().BeTrue();
        inner.Calls.Should().Equal("store:twitch:token-1", "get:twitch", "has:twitch", "clear:twitch");
    }

    [Fact]
    public async Task Given_Bridge_When_EachOperationRuns_Then_FreshScopeIsOpenedPerCall()
    {
        var inner = new RecordingInnerStore();
        await using var provider = BuildProvider(inner);
        var bridge = new ScopedOAuthTokenStore(provider.GetRequiredService<IServiceScopeFactory>());

        await bridge.StoreRefreshTokenAsync("twitch", "token-1", TestContext.Current.CancellationToken);
        await bridge.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);
        await bridge.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);

        // Scoped lifetime: a new ScopeProbe instance per scope. Three calls →
        // three distinct probe instances proves three distinct scopes.
        ScopeProbe.InstanceCount.Should().BeGreaterThanOrEqualTo(3);
    }

    private static ServiceProvider BuildProvider(RecordingInnerStore inner)
    {
        ScopeProbe.InstanceCount = 0;
        var services = new ServiceCollection();
        services.AddScoped<ScopeProbe>();
        services.AddScoped<IOAuthTokenStore>(sp =>
        {
            _ = sp.GetRequiredService<ScopeProbe>();
            return inner;
        });
        return services.BuildServiceProvider();
    }

    private sealed class ScopeProbe
    {
        public static int InstanceCount;

        public ScopeProbe()
        {
            Interlocked.Increment(ref InstanceCount);
        }
    }

    private sealed class RecordingInnerStore : IOAuthTokenStore
    {
        private string? _token;

        public List<string> Calls { get; } = [];

        public Task StoreRefreshTokenAsync(string platform, string rawToken, CancellationToken cancellationToken = default)
        {
            Calls.Add($"store:{platform}:{rawToken}");
            _token = rawToken;
            return Task.CompletedTask;
        }

        public Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            Calls.Add($"get:{platform}");
            return Task.FromResult(_token);
        }

        public Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            Calls.Add($"has:{platform}");
            return Task.FromResult(!string.IsNullOrWhiteSpace(_token));
        }

        public Task ClearRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            Calls.Add($"clear:{platform}");
            _token = null;
            return Task.CompletedTask;
        }
    }
}
