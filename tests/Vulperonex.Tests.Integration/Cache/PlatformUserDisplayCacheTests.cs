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
}
