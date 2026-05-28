using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WipeWorkflowRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM WorkflowRules;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
