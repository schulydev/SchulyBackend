using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReadPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_ClassId",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_Date",
                table: "AgendaEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_ClassId_Date",
                table: "AgendaEntries",
                columns: new[] { "ClassId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_ClassId_Date",
                table: "AgendaEntries");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_ClassId",
                table: "AgendaEntries",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_Date",
                table: "AgendaEntries",
                column: "Date");
        }
    }
}
