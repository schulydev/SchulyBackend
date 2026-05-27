using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExamGradeDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_ClassId",
                table: "Exams");

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

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "Exams",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Exams",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_ClassId_Date",
                table: "Exams",
                columns: new[] { "ClassId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_ClassId_Date",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "ClassAverage",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_ClassId",
                table: "Exams",
                column: "ClassId");
        }
    }
}
