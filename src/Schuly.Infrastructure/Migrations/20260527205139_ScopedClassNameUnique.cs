using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopedClassNameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_Name",
                table: "Classes");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolId_Name",
                table: "Classes",
                columns: new[] { "SchoolId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_SchoolId_Name",
                table: "Classes");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Name",
                table: "Classes",
                column: "Name",
                unique: true);
        }
    }
}
