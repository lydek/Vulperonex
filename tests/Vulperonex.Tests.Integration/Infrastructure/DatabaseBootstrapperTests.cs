using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Data;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class DatabaseBootstrapperTests
{
    [Fact]
    public async Task Given_NewSqliteDatabase_When_Bootstrapped_Then_AutoVacuumIsFull()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        var bootstrapper = new DatabaseBootstrapper(context);

        await bootstrapper.MigrateAsync(TestContext.Current.CancellationToken);

        var autoVacuum = await QueryLongAsync(fixture.Connection, "PRAGMA auto_vacuum;");
        autoVacuum.Should().Be(2);
    }

    private static async Task<long> QueryLongAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt64(result);
    }
}
