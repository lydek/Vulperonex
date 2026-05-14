using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Vulperonex.Infrastructure.Migrations;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.Migrations;

public sealed class MigrationClassifierTests
{
    [Theory]
    [InlineData("DROP TABLE Members;")]
    [InlineData("ALTER TABLE Members RENAME TO OldMembers;")]
    [InlineData("DELETE FROM Members;")]
    [InlineData("TRUNCATE TABLE Members;")]
    public void Given_DestructiveRawSql_When_Classified_Then_ResultIsDestructive(string sql)
    {
        var migrationBuilder = new MigrationBuilder("Sqlite");
        migrationBuilder.Sql(sql);

        var result = MigrationClassifier.Classify(migrationBuilder.Operations);

        result.Should().Be(MigrationRisk.Destructive);
    }

    [Fact]
    public void Given_AlterRawSql_When_Classified_Then_ResultRequiresReview()
    {
        var migrationBuilder = new MigrationBuilder("Sqlite");
        migrationBuilder.Sql("ALTER TABLE Members ADD COLUMN Notes TEXT;");

        var result = MigrationClassifier.Classify(migrationBuilder.Operations);

        result.Should().Be(MigrationRisk.ReviewRequired);
    }

    [Fact]
    public void Given_CreateOnlyOperations_When_Classified_Then_ResultIsSafe()
    {
        var migrationBuilder = new MigrationBuilder("Sqlite");
        migrationBuilder.CreateTable(
            name: "Example",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Example", x => x.Id);
            });

        var result = MigrationClassifier.Classify(migrationBuilder.Operations);

        result.Should().Be(MigrationRisk.Safe);
    }
}
