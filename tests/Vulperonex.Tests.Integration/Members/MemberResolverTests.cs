using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
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

    [Fact]
    public async Task Given_MemberListPage_When_Loaded_Then_RelatedDataIsFetchedInBatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var counter = new CommandCounterInterceptor();
        var options = new DbContextOptionsBuilder<VulperonexDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;

        await using (var seedContext = new VulperonexDbContext(options))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
            for (var index = 0; index < 5; index++)
            {
                var memberId = $"member-{index}";
                seedContext.Members.Add(new MemberEntity
                {
                    MemberId = memberId,
                    CheckInCount = index,
                    TotalLoyalty = index * 10,
                });
                seedContext.PlatformIdentities.Add(new PlatformIdentityEntity
                {
                    MemberId = memberId,
                    Platform = "twitch",
                    PlatformUserId = $"user-{index}",
                });
            }

            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        counter.Reset();
        await using var context = new VulperonexDbContext(options);
        var service = new MemberQueryService(context);

        var results = await service.ListAsync(limit: 5, cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(5);
        counter.CommandCount.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Given_ResolvedMember_When_CheckInIncremented_Then_CountIsPersisted()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var resolver = new MemberResolver(context);
        var repository = new MemberStreamStateRepository(context);
        var identity = PlatformIdentity.Create("twitch", "alice");
        await resolver.ResolveMemberIdAsync(identity, TestContext.Current.CancellationToken);

        var first = await repository.IncrementCheckInAsync(identity, TestContext.Current.CancellationToken);
        var second = await repository.IncrementCheckInAsync(identity, TestContext.Current.CancellationToken);

        first.Should().Be(1);
        second.Should().Be(2);
        var member = await context.Members.SingleAsync(TestContext.Current.CancellationToken);
        member.CheckInCount.Should().Be(2);
    }

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        public int CommandCount { get; private set; }

        public void Reset()
        {
            CommandCount = 0;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CommandCount++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CommandCount++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
