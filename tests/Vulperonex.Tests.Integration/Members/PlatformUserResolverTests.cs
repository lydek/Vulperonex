using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Twitch;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Members;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class PlatformUserResolverTests
{
    [Fact]
    public async Task Given_DuplicateDisplayNames_When_ResolvingDisplayName_Then_LocalMatchIsNotUsed()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        context.PlatformUserDisplayInfo.AddRange(
            new PlatformUserDisplayInfoEntity
            {
                Platform = "twitch",
                PlatformUserId = "user-1",
                DisplayName = "SameName",
                Login = "first_login",
                FetchedAt = DateTimeOffset.UtcNow,
            },
            new PlatformUserDisplayInfoEntity
            {
                Platform = "twitch",
                PlatformUserId = "user-2",
                DisplayName = "SameName",
                Login = "second_login",
                FetchedAt = DateTimeOffset.UtcNow,
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var helix = new RecordingHelixClient();
        var resolver = new PlatformUserResolver(context, helix);

        var result = await resolver.ResolveAsync("twitch", "@SameName", TestContext.Current.CancellationToken);

        result.IsFound.Should().BeFalse();
        result.Login.Should().BeEmpty();
        helix.SearchQueries.Should().ContainSingle().Which.Should().Be("SameName");
    }

    private sealed class RecordingHelixClient : IHelixClient
    {
        public List<string> SearchQueries { get; } = [];

        public Task<PlatformUserProfile?> LookupUserAsync(
            string? login,
            string? userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PlatformUserProfile?>(null);
        }

        public Task<PlatformUserProfile?> SearchChannelExactAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            SearchQueries.Add(query);
            return Task.FromResult<PlatformUserProfile?>(null);
        }

        public Task<PlatformShoutoutResult> SendShoutoutAsync(
            string targetLogin,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlatformShoutoutResult(false, targetLogin, null, null));
        }

        public Task<bool> RefundRedemptionAsync(
            string rewardId,
            string redemptionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<PlatformBadgeDescriptor>> GetGlobalBadgesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformBadgeDescriptor>>([]);
        }

        public Task<IReadOnlyList<PlatformBadgeDescriptor>> GetChannelBadgesAsync(
            string broadcasterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformBadgeDescriptor>>([]);
        }

        public Task CreateEventSubSubscriptionAsync(
            string type,
            string version,
            IReadOnlyDictionary<string, string> condition,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlatformRewardDescriptor>> GetCustomRewardsAsync(
            string broadcasterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformRewardDescriptor>>([]);
        }
    }
}
