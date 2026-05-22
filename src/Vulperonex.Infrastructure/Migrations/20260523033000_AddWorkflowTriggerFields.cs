using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260523033000_AddWorkflowTriggerFields")]
public partial class AddWorkflowTriggerFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TriggerJson",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MatchCondition",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TriggerJson", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "MatchCondition", table: "WorkflowRules");
    }
}
