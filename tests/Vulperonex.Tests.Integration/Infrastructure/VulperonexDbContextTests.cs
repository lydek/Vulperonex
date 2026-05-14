using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class VulperonexDbContextTests
{
    [Fact]
    public async Task Given_TempSqliteDatabase_When_ContextIsCreated_Then_ConnectionCanBeOpened()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();

        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task Given_VulperonexDbContext_When_ModelIsInspected_Then_PhaseTwoDbSetsArePresent()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();

        var entityNames = context.Model
            .GetEntityTypes()
            .Select(entity => entity.ClrType.Name)
            .ToArray();

        entityNames.Should().BeEquivalentTo(
            nameof(MemberEntity),
            nameof(PlatformIdentityEntity),
            nameof(WorkflowRuleEntity),
            nameof(SystemSettingEntity),
            nameof(AppLogEntity),
            nameof(PlatformUserDisplayInfoEntity),
            nameof(TransientDeliveryQueueEntity),
            nameof(ActionExecutionLogEntity));
    }
}
