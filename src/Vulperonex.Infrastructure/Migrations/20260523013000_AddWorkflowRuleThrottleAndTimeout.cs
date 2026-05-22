using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260523013000_AddWorkflowRuleThrottleAndTimeout")]
public partial class AddWorkflowRuleThrottleAndTimeout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ThrottleJson",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<int>(
            name: "TimeoutSeconds",
            table: "WorkflowRules",
            type: "INTEGER",
            nullable: false,
            defaultValue: 30);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ThrottleJson", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "TimeoutSeconds", table: "WorkflowRules");
    }
}
