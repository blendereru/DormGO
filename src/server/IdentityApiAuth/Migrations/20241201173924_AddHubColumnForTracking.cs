using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityApiAuth.Migrations
{
    /// <inheritdoc />
    public partial class AddHubColumnForTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hub",
                table: "UserConnections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hub",
                table: "UserConnections");
        }
    }
}
