using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data.Entities;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class SchemaTests
{
    [Fact]
    public async Task Given_InitialSchemaMigration_When_Applied_Then_RequiredTablesAndColumnsExist()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var systemSettingsColumns = await QueryScalarValuesAsync(
            fixture.Connection,
            "SELECT name FROM pragma_table_info('SystemSettings') ORDER BY name;");
        systemSettingsColumns.Should().BeEquivalentTo("Category", "Key", "UpdatedAt", "Value");

        var workflowRuleColumns = await QueryScalarValuesAsync(
            fixture.Connection,
            "SELECT name FROM pragma_table_info('WorkflowRules') ORDER BY name;");
        workflowRuleColumns.Should().Contain(["ActionsJson", "ConditionsJson", "ThrottleJson", "TimeoutSeconds"]);

        var workflowJsonColumnTypes = await QueryScalarValuesAsync(
            fixture.Connection,
            """
            SELECT type
            FROM pragma_table_info('WorkflowRules')
            WHERE name IN ('ActionsJson', 'ConditionsJson', 'ThrottleJson')
            ORDER BY name;
            """);
        workflowJsonColumnTypes.Should().OnlyContain(columnType => columnType == "TEXT");
    }

    [Fact]
    public async Task Given_InitialSchemaMigration_When_Applied_Then_PlatformIdentityHasUniqueConstraint()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        context.Members.Add(new MemberEntity { MemberId = "member-1" });
        context.PlatformIdentities.Add(new PlatformIdentityEntity
        {
            MemberId = "member-1",
            Platform = "twitch",
            PlatformUserId = "user-1",
        });
        context.PlatformIdentities.Add(new PlatformIdentityEntity
        {
            MemberId = "member-1",
            Platform = "twitch",
            PlatformUserId = "user-1",
        });

        var act = async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Given_InitialSchemaMigration_When_Applied_Then_PlatformUserDisplayInfoUsesCompositePrimaryKey()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var primaryKeyColumns = await QueryScalarValuesAsync(
            fixture.Connection,
            """
            SELECT name
            FROM pragma_table_info('PlatformUserDisplayInfo')
            WHERE pk > 0
            ORDER BY pk;
            """);

        primaryKeyColumns.Should().Equal("Platform", "PlatformUserId");
    }

    private static async Task<string[]> QueryScalarValuesAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }
}
