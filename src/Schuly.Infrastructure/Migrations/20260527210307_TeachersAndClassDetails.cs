using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeachersAndClassDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassTeacher",
                columns: table => new
                {
                    ClassesId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeachersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTeacher", x => new { x.ClassesId, x.TeachersId });
                    table.ForeignKey(
                        name: "FK_ClassTeacher_Classes_ClassesId",
                        column: x => x.ClassesId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassTeacher_Teachers_TeachersId",
                        column: x => x.TeachersId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolId_SchoolYearStart_SemesterHalf",
                table: "Classes",
                columns: new[] { "SchoolId", "SchoolYearStart", "SemesterHalf" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Class_SemesterHalf",
                table: "Classes",
                sql: "\"SemesterHalf\" IS NULL OR \"SemesterHalf\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeacher_TeachersId",
                table: "ClassTeacher",
                column: "TeachersId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_SchoolId_Code",
                table: "Teachers",
                columns: new[] { "SchoolId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassTeacher");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropIndex(
                name: "IX_Classes_SchoolId_SchoolYearStart_SemesterHalf",
                table: "Classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Class_SemesterHalf",
                table: "Classes");

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
        }
    }
}
