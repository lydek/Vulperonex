using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

[Migration("20260516161000_ExtendWorkflowRuleApiFields")]
public partial class ExtendWorkflowRuleApiFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: false,
            defaultValue: DateTimeOffset.UnixEpoch);

        migrationBuilder.AddColumn<string>(
            name: "EventTypeKey",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: false,
            defaultValue: string.Empty);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionMode",
            table: "WorkflowRules",
            type: "TEXT",
            nullable: false,
            defaultValue: "Serial");

        migrationBuilder.AddColumn<int>(
            name: "MaxParallelism",
            table: "WorkflowRules",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "WorkflowRules",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreatedAt", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "EventTypeKey", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "ExecutionMode", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "MaxParallelism", table: "WorkflowRules");
        migrationBuilder.DropColumn(name: "Priority", table: "WorkflowRules");
    }
}
