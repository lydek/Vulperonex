using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260523050000_AddCounters")]
public partial class AddCounters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Counters",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Counters", x => x.Key);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Counters");
    }
}
