using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormGO.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateColumnToMessageEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Messages");
        }
    }
}
