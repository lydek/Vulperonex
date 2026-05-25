using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberAuditLogSubjectKindIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MemberAuditLogs_MemberId_OccurredAt;");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAuditLogs_MemberId_SubjectKind_OccurredAt",
                table: "MemberAuditLogs",
                columns: new[] { "MemberId", "SubjectKind", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_MemberAuditLogs_MemberId_SubjectKind_OccurredAt;");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAuditLogs_MemberId_OccurredAt",
                table: "MemberAuditLogs",
                columns: new[] { "MemberId", "OccurredAt" });
        }
    }
}
