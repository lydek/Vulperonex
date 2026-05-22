using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260522130000_AddAppLogStructuredFields")]
public partial class AddAppLogStructuredFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Exception",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EventTypeKey",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Platform",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MemberId",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WorkflowRuleId",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActionType",
            table: "AppLogs",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppLogs_CreatedAt",
            table: "AppLogs",
            column: "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AppLogs_CreatedAt", table: "AppLogs");
        migrationBuilder.DropColumn(name: "ActionType", table: "AppLogs");
        migrationBuilder.DropColumn(name: "WorkflowRuleId", table: "AppLogs");
        migrationBuilder.DropColumn(name: "MemberId", table: "AppLogs");
        migrationBuilder.DropColumn(name: "Platform", table: "AppLogs");
        migrationBuilder.DropColumn(name: "EventTypeKey", table: "AppLogs");
        migrationBuilder.DropColumn(name: "Exception", table: "AppLogs");
    }
}
