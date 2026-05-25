using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberAuditLogPreventUpdateTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER PreventUpdate_MemberAuditLogs
BEFORE UPDATE ON MemberAuditLogs
BEGIN
    SELECT RAISE(ABORT, 'Updates are prohibited on append-only table MemberAuditLogs.');
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS PreventUpdate_MemberAuditLogs;");
        }
    }
}
