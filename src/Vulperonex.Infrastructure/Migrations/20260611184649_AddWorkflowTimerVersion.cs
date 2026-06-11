using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTimerVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WorkflowTimers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkflowTimers");
        }
    }
}
