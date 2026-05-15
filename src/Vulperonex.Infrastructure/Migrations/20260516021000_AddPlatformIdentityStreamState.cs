using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations;

public partial class AddPlatformIdentityStreamState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFollower",
            table: "PlatformIdentities",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSubscriber",
            table: "PlatformIdentities",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "SubscriptionTier",
            table: "PlatformIdentities",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsFollower", table: "PlatformIdentities");
        migrationBuilder.DropColumn(name: "IsSubscriber", table: "PlatformIdentities");
        migrationBuilder.DropColumn(name: "SubscriptionTier", table: "PlatformIdentities");
    }
}
