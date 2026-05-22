using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260523023000_AddWorkflowRuleOnFailureActions")]
public partial class AddWorkflowRuleOnFailureActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OnFailureActionsJson",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OnFailureActionsJson", table: "WorkflowRules");
    }
}
