using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Members;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class MemberResolverTests
{
    [Fact]
    public async Task Given_ConcurrentResolveForSameIdentity_When_Completed_Then_OnlyOneMemberIsCreated()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var resolver = new MemberResolver(context);
        var identity = PlatformIdentity.Create("twitch", "user-1");

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => resolver.ResolveMemberIdAsync(identity, TestContext.Current.CancellationToken)));

        results.Should().OnlyContain(memberId => memberId == results[0]);
        results[0].Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{26}$");
        var memberCount = await context.Members.CountAsync(TestContext.Current.CancellationToken);
        var identityCount = await context.PlatformIdentities.CountAsync(TestContext.Current.CancellationToken);
        memberCount.Should().Be(1);
        identityCount.Should().Be(1);
    }
}
