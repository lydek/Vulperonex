using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260523043000_AddWorkflowSubWorkflowFlag")]
public partial class AddWorkflowSubWorkflowFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSubWorkflow",
            table: "WorkflowRules",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsSubWorkflow", table: "WorkflowRules");
    }
}
