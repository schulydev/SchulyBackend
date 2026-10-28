using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToamaisutaa011 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LockoutStrikes",
                table: "ToamaisutaaUserTwoFactors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DisabledAt",
                table: "ToamaisutaaUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LockoutStrikes",
                table: "ToamaisutaaPasswordCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ToamaisutaaSignInThrottles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    FirstFailedAttemptAt = table.Column<long>(type: "bigint", nullable: true),
                    LockedOutUntil = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToamaisutaaSignInThrottles", x => new { x.UserId, x.Source });
                    table.ForeignKey(
                        name: "FK_ToamaisutaaSignInThrottles_ToamaisutaaUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ToamaisutaaUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToamaisutaaSignInThrottles");

            migrationBuilder.DropColumn(
                name: "LockoutStrikes",
                table: "ToamaisutaaUserTwoFactors");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "ToamaisutaaUsers");

            migrationBuilder.DropColumn(
                name: "LockoutStrikes",
                table: "ToamaisutaaPasswordCredentials");
        }
    }
}
