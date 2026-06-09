using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulperonex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformUserLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "PlatformUserDisplayInfo",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Login",
                table: "PlatformUserDisplayInfo");
        }
    }
}
