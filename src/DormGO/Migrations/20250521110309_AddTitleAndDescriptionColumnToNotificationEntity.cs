using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormGO.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleAndDescriptionColumnToNotificationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Message",
                table: "PostNotifications",
                newName: "Title");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PostNotifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "PostNotifications");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "PostNotifications",
                newName: "Message");
        }
    }
}
