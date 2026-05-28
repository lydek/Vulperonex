using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateWorkflowRuleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EventTypeKey",
                table: "WorkflowRules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            // 1. Lift inner EventTypeKey and MatchCondition to outer table columns if outer is empty/null
            migrationBuilder.Sql("UPDATE WorkflowRules SET EventTypeKey = COALESCE(NULLIF(EventTypeKey, ''), json_extract(TriggerJson, '$.eventTypeKey')) WHERE TriggerJson IS NOT NULL AND json_extract(TriggerJson, '$.eventTypeKey') IS NOT NULL;");
            migrationBuilder.Sql("UPDATE WorkflowRules SET MatchCondition = COALESCE(NULLIF(MatchCondition, ''), json_extract(TriggerJson, '$.matchCondition')) WHERE TriggerJson IS NOT NULL AND json_extract(TriggerJson, '$.matchCondition') IS NOT NULL;");

            // 2. Prune obsolete inner eventTypeKey and matchCondition properties from TriggerJson blob
            migrationBuilder.Sql("UPDATE WorkflowRules SET TriggerJson = json_remove(TriggerJson, '$.eventTypeKey', '$.matchCondition') WHERE TriggerJson IS NOT NULL;");

            // 3. Nullify EventTypeKey for all sub-workflow rules
            migrationBuilder.Sql("UPDATE WorkflowRules SET EventTypeKey = NULL WHERE IsSubWorkflow = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fill EventTypeKey with empty string for sub-workflows/null rows before restoring NOT NULL constraint
            migrationBuilder.Sql("UPDATE WorkflowRules SET EventTypeKey = '' WHERE EventTypeKey IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "EventTypeKey",
                table: "WorkflowRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
