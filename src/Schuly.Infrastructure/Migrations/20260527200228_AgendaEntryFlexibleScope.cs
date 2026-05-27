using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgendaEntryFlexibleScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClassId",
                table: "AgendaEntries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "AgendaEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolUserId",
                table: "AgendaEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolId",
                table: "AgendaEntries",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolUserId",
                table: "AgendaEntries",
                column: "SchoolUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgendaEntry_ExactlyOneScope",
                table: "AgendaEntries",
                sql: "(CASE WHEN \"ClassId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"SchoolId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"SchoolUserId\" IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_AgendaEntries_SchoolUsers_SchoolUserId",
                table: "AgendaEntries",
                column: "SchoolUserId",
                principalTable: "SchoolUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgendaEntries_Schools_SchoolId",
                table: "AgendaEntries",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgendaEntries_SchoolUsers_SchoolUserId",
                table: "AgendaEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_AgendaEntries_Schools_SchoolId",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolId",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolUserId",
                table: "AgendaEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AgendaEntry_ExactlyOneScope",
                table: "AgendaEntries");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AgendaEntries");

            migrationBuilder.DropColumn(
                name: "SchoolUserId",
                table: "AgendaEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClassId",
                table: "AgendaEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
