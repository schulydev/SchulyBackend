using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoreScopeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolUsers_SchoolId",
                table: "SchoolUsers");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolId",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolUserId",
                table: "AgendaEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolUsers_SchoolId_Role",
                table: "SchoolUsers",
                columns: new[] { "SchoolId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolId_Date",
                table: "AgendaEntries",
                columns: new[] { "SchoolId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolUserId_Date",
                table: "AgendaEntries",
                columns: new[] { "SchoolUserId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolUsers_SchoolId_Role",
                table: "SchoolUsers");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolId_Date",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolUserId_Date",
                table: "AgendaEntries");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolUsers_SchoolId",
                table: "SchoolUsers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolId",
                table: "AgendaEntries",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolUserId",
                table: "AgendaEntries",
                column: "SchoolUserId");
        }
    }
}
