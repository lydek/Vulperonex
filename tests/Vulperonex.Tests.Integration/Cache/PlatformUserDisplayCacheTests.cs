using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Infrastructure.Cache;
using Vulperonex.Infrastructure.Data.Entities;
using Xunit;

namespace Vulperonex.Tests.Integration.Cache;

public sealed class PlatformUserDisplayCacheTests
{
    [Fact]
    public async Task Given_L1Hit_When_GetAsync_Then_L2ChangesAreNotRead()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new PlatformUserDisplayCache(context);
        await cache.UpdateAsync("twitch", "user-1", displayInfo => displayInfo with { DisplayName = "L1" }, TestContext.Current.CancellationToken);

        var row = await context.PlatformUserDisplayInfo.FindAsync(["twitch", "user-1"], TestContext.Current.CancellationToken);
        row!.DisplayName = "L2";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var displayInfo = await cache.GetAsync("twitch", "user-1", TestContext.Current.CancellationToken);

        displayInfo!.DisplayName.Should().Be("L1");
    }

    [Fact]
    public async Task Given_L1Miss_When_GetAsync_Then_L2ValueBackfillsL1()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        context.PlatformUserDisplayInfo.Add(new PlatformUserDisplayInfoEntity
        {
            Platform = "twitch",
            PlatformUserId = "user-1",
            DisplayName = "From L2",
            BadgesJson = """["vip"]""",
            FetchedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var cache = new PlatformUserDisplayCache(context);

        var first = await cache.GetAsync("twitch", "user-1", TestContext.Current.CancellationToken);
        var row = await context.PlatformUserDisplayInfo.FindAsync(["twitch", "user-1"], TestContext.Current.CancellationToken);
        row!.DisplayName = "Changed L2";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var second = await cache.GetAsync("twitch", "user-1", TestContext.Current.CancellationToken);

        first!.DisplayName.Should().Be("From L2");
        second!.DisplayName.Should().Be("From L2");
        second.Badges.Should().ContainSingle().Which.Should().Be("vip");
    }

    [Fact]
    public async Task Given_DisplayInfoUpdate_When_StateChanges_Then_AbsoluteValuesReplaceExistingValues()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new PlatformUserDisplayCache(context);

        await cache.UpdateAsync("twitch", "user-1", displayInfo => displayInfo with { TotalBitsGiven = 100 }, TestContext.Current.CancellationToken);
        var updated = await cache.UpdateAsync("twitch", "user-1", displayInfo => displayInfo with { TotalBitsGiven = 20 }, TestContext.Current.CancellationToken);

        updated.TotalBitsGiven.Should().Be(20);
    }

    [Fact]
    public async Task Given_CacheMiss_When_UpdateAsyncRuns_Then_DefaultRowIsCreatedAndUpdaterIsApplied()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new PlatformUserDisplayCache(context);

        var updated = await cache.UpdateAsync("twitch", "user-1", displayInfo => displayInfo with
        {
            DisplayName = "Created",
        }, TestContext.Current.CancellationToken);

        updated.DisplayName.Should().Be("Created");
        updated.AvatarUrl.Should().BeNull();
        updated.ColorHex.Should().BeNull();
        updated.Badges.Should().BeEmpty();
        updated.IsSubscriber.Should().BeFalse();
        updated.SubscriptionTier.Should().BeNull();
        updated.TotalBitsGiven.Should().Be(0);
        updated.FetchedAt.Should().BeAfter(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Given_LoginWritten_When_GetAsync_Then_LoginRoundTrips()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var cache = new PlatformUserDisplayCache(context);

        await cache.UpdateAsync(
            "twitch",
            "109565589",
            displayInfo => displayInfo with { DisplayName = "RotanFox", Login = "rotanfox" },
            TestContext.Current.CancellationToken);

        var fromL1 = await cache.GetAsync("twitch", "109565589", TestContext.Current.CancellationToken);
        fromL1!.Login.Should().Be("rotanfox");

        await using var freshContext = await fixture.CreateContextAsync();
        var freshCache = new PlatformUserDisplayCache(freshContext);
        var fromL2 = await freshCache.GetAsync("twitch", "109565589", TestContext.Current.CancellationToken);
        fromL2!.Login.Should().Be("rotanfox");
    }

    [Fact]
    public async Task Given_ExpiredRows_When_CleanupRuns_Then_ExpiredRowsAreDeletedAndFreshRowsRemain()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        context.PlatformUserDisplayInfo.AddRange(
            new PlatformUserDisplayInfoEntity
            {
                Platform = "twitch",
                PlatformUserId = "expired",
                BadgesJson = "[]",
                FetchedAt = DateTimeOffset.UtcNow.AddHours(-25),
            },
            new PlatformUserDisplayInfoEntity
            {
                Platform = "twitch",
                PlatformUserId = "fresh",
                BadgesJson = "[]",
                FetchedAt = DateTimeOffset.UtcNow,
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var worker = new PlatformUserDisplayCacheCleanupWorker(context);

        var deleted = await worker.CleanupExpiredAsync(TestContext.Current.CancellationToken);

        deleted.Should().Be(1);
        (await context.PlatformUserDisplayInfo.FindAsync(["twitch", "expired"], TestContext.Current.CancellationToken)).Should().BeNull();
        (await context.PlatformUserDisplayInfo.FindAsync(["twitch", "fresh"], TestContext.Current.CancellationToken)).Should().NotBeNull();
    }
}
