using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Members;
using Vulperonex.Infrastructure.Logging;
using Xunit;

namespace Vulperonex.Tests.Integration.Members;

public sealed class MemberAuditLogRepositoryTests
{
    [Fact]
    public async Task Given_MemberAuditLog_When_Appended_Then_IsPersistedAndQueryable()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Seed a member to satisfy FK constraint
        var memberId = "sim-member-id-123";
        context.Members.Add(new MemberEntity
        {
            MemberId = memberId,
            CheckInCount = 1,
            TotalLoyalty = 10
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new MemberAuditLogRepository(context);
        var log = new MemberAuditLog
        {
            MemberId = memberId,
            ActorKind = "user",
            ActorId = "admin-1",
            Operation = "adjust_loyalty",
            BeforeJson = "{}",
            AfterJson = "{\"loyalty\": 10}",
            Reason = "Manual adjustment"
        };

        await repository.AppendAsync(log, TestContext.Current.CancellationToken);

        var results = await repository.QueryAsync(memberId, limit: 10, offset: 0, TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        var result = results[0];
        result.Id.Should().NotBeNullOrEmpty();
        result.Id.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{26}$"); // Valid ULID
        result.MemberId.Should().Be(memberId);
        result.ActorKind.Should().Be("user");
        result.ActorId.Should().Be("admin-1");
        result.Operation.Should().Be("adjust_loyalty");
        result.Reason.Should().Be("Manual adjustment");
    }

    [Fact]
    public async Task Given_MultipleAuditLogs_When_Queried_Then_ReturnedInDescendingOrderAndPaged()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var memberId = "sim-member-id-456";
        context.Members.Add(new MemberEntity
        {
            MemberId = memberId,
            CheckInCount = 5,
            TotalLoyalty = 50
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new MemberAuditLogRepository(context);
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            var log = new MemberAuditLog
            {
                Id = $"01ARZ3NDEKTSV4RRFFQ61A5QR{i}",
                MemberId = memberId,
                ActorKind = "system",
                Operation = "adjust_loyalty",
                OccurredAt = baseTime.AddMinutes(i),
                Reason = $"Log {i}"
            };
            await repository.AppendAsync(log, TestContext.Current.CancellationToken);
        }

        // Query descending order (should be: Log 4, Log 3, Log 2)
        var page1 = await repository.QueryAsync(memberId, limit: 3, offset: 0, TestContext.Current.CancellationToken);
        page1.Should().HaveCount(3);
        page1[0].Reason.Should().Be("Log 4");
        page1[1].Reason.Should().Be("Log 3");
        page1[2].Reason.Should().Be("Log 2");

        // Query offset (should be: Log 1, Log 0)
        var page2 = await repository.QueryAsync(memberId, limit: 3, offset: 3, TestContext.Current.CancellationToken);
        page2.Should().HaveCount(2);
        page2[0].Reason.Should().Be("Log 1");
        page2[1].Reason.Should().Be("Log 0");
    }
}
