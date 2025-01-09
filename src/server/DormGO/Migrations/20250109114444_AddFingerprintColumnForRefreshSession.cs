﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormGO.Migrations
{
    /// <inheritdoc />
    public partial class AddFingerprintColumnForRefreshSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "RefreshSessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "RefreshSessions");
        }
    }
}
