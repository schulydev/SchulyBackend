using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SchemaCleanupDropDeadColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_SchoolId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Classes_SchoolId_SchoolYearStart_SemesterHalf",
                table: "Classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Class_SemesterHalf",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "ClassAverage",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "SchoolYearStart",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "SemesterHalf",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Classes");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers");

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "Grades",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClassAverage",
                table: "Exams",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Classes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolYearStart",
                table: "Classes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemesterHalf",
                table: "Classes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Classes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolId",
                table: "Classes",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolId_SchoolYearStart_SemesterHalf",
                table: "Classes",
                columns: new[] { "SchoolId", "SchoolYearStart", "SemesterHalf" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Class_SemesterHalf",
                table: "Classes",
                sql: "\"SemesterHalf\" IS NULL OR \"SemesterHalf\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers",
                column: "Email",
                unique: true);
        }
    }
}
