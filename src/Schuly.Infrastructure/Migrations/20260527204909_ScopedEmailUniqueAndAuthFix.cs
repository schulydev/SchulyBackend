using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopedEmailUniqueAndAuthFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolUsers_Email",
                table: "SchoolUsers");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolUsers_SchoolId_Email",
                table: "SchoolUsers",
                columns: new[] { "SchoolId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolUsers_SchoolId_Email",
                table: "SchoolUsers");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolUsers_Email",
                table: "SchoolUsers",
                column: "Email",
                unique: true);
        }
    }
}
