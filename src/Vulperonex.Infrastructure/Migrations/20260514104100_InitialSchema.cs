using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Level = table.Column<string>(type: "TEXT", nullable: false),
                Message = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Members",
            columns: table => new
            {
                MemberId = table.Column<string>(type: "TEXT", nullable: false),
                TotalLoyalty = table.Column<int>(type: "INTEGER", nullable: false),
                CheckInCount = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Members", x => x.MemberId);
            });

        migrationBuilder.CreateTable(
            name: "PlatformUserDisplayInfo",
            columns: table => new
            {
                Platform = table.Column<string>(type: "TEXT", nullable: false),
                PlatformUserId = table.Column<string>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                AvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                ColorHex = table.Column<string>(type: "TEXT", nullable: true),
                BadgesJson = table.Column<string>(type: "TEXT", nullable: false),
                IsSubscriber = table.Column<bool>(type: "INTEGER", nullable: false),
                SubscriptionTier = table.Column<string>(type: "TEXT", nullable: true),
                TotalBitsGiven = table.Column<long>(type: "INTEGER", nullable: false),
                FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlatformUserDisplayInfo", x => new { x.Platform, x.PlatformUserId });
            });

        migrationBuilder.CreateTable(
            name: "SystemSettings",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<string>(type: "TEXT", nullable: false),
                Category = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemSettings", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "WorkflowRules",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                ConditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                ActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowRules", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PlatformIdentities",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                MemberId = table.Column<string>(type: "TEXT", nullable: false),
                Platform = table.Column<string>(type: "TEXT", nullable: false),
                PlatformUserId = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlatformIdentities", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlatformIdentities_Members_MemberId",
                    column: x => x.MemberId,
                    principalTable: "Members",
                    principalColumn: "MemberId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformIdentities_MemberId",
            table: "PlatformIdentities",
            column: "MemberId");

        migrationBuilder.CreateIndex(
            name: "IX_PlatformIdentities_Platform_PlatformUserId",
            table: "PlatformIdentities",
            columns: new[] { "Platform", "PlatformUserId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AppLogs");
        migrationBuilder.DropTable(name: "PlatformIdentities");
        migrationBuilder.DropTable(name: "PlatformUserDisplayInfo");
        migrationBuilder.DropTable(name: "SystemSettings");
        migrationBuilder.DropTable(name: "WorkflowRules");
        migrationBuilder.DropTable(name: "Members");
    }
}
