using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StudentDocumentsAndSemesterReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SemesterReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchoolYearStart = table.Column<int>(type: "integer", nullable: false),
                    SemesterHalf = table.Column<int>(type: "integer", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromotionDecision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GradeAverage = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    InsufficientGradeCount = table.Column<int>(type: "integer", nullable: true),
                    DeficiencyPoints = table.Column<int>(type: "integer", nullable: true),
                    ExcusedAbsences = table.Column<int>(type: "integer", nullable: true),
                    UnexcusedAbsences = table.Column<int>(type: "integer", nullable: true),
                    TotalAbsences = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemesterReports", x => x.Id);
                    table.CheckConstraint("CK_SemesterReport_SemesterHalf", "\"SemesterHalf\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_SemesterReports_SchoolUsers_SchoolUserId",
                        column: x => x.SchoolUserId,
                        principalTable: "SchoolUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnteredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FollowUpAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDocuments_SchoolUsers_SchoolUserId",
                        column: x => x.SchoolUserId,
                        principalTable: "SchoolUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SemesterSubjectGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SemesterReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectTypeMarker = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Grade = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Marker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemesterSubjectGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemesterSubjectGrades_SemesterReports_SemesterReportId",
                        column: x => x.SemesterReportId,
                        principalTable: "SemesterReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemesterReports_SchoolUserId_ProgramCode_SchoolYearStart_Se~",
                table: "SemesterReports",
                columns: new[] { "SchoolUserId", "ProgramCode", "SchoolYearStart", "SemesterHalf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SemesterSubjectGrades_SemesterReportId_SubjectCode",
                table: "SemesterSubjectGrades",
                columns: new[] { "SemesterReportId", "SubjectCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentDocuments_SchoolUserId_Category",
                table: "StudentDocuments",
                columns: new[] { "SchoolUserId", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemesterSubjectGrades");

            migrationBuilder.DropTable(
                name: "StudentDocuments");

            migrationBuilder.DropTable(
                name: "SemesterReports");
        }
    }
}
