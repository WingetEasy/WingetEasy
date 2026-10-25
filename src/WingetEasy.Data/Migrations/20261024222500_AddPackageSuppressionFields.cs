using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WingetEasy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSuppressionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeferredUntilUtc",
                table: "Packages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IgnoredVersion",
                table: "Packages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeferredUntilUtc",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IgnoredVersion",
                table: "Packages");
        }
    }
}

