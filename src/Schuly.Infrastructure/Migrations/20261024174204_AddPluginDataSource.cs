using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginDataSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Synced rows written before Source/ExternalId existed can't be matched by a
            // resync and would be duplicated, so they are dropped and plugins resync them.
            migrationBuilder.Sql("""
                DELETE FROM "NotificationOutbox";
                DELETE FROM "Grades";
                DELETE FROM "Absences";
                DELETE FROM "AgendaEntries";
                DELETE FROM "StudentDocuments";
                DELETE FROM "Exams";
                DELETE FROM "Classes";
                """);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "StudentDocuments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "StudentDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "SchoolUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Grades",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Grades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Exams",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Classes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "AgendaEntries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "AgendaEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Absences",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Absences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentDocuments_SchoolUserId_Source_ExternalId",
                table: "StudentDocuments",
                columns: new[] { "SchoolUserId", "Source", "ExternalId" },
                unique: true,
                filter: "\"Source\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Grades_SchoolUserId_Source_ExternalId",
                table: "Grades",
                columns: new[] { "SchoolUserId", "Source", "ExternalId" },
                unique: true,
                filter: "\"Source\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaEntries_SchoolUserId_Source_ExternalId",
                table: "AgendaEntries",
                columns: new[] { "SchoolUserId", "Source", "ExternalId" },
                unique: true,
                filter: "\"Source\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Absences_SchoolUserId_Source_ExternalId",
                table: "Absences",
                columns: new[] { "SchoolUserId", "Source", "ExternalId" },
                unique: true,
                filter: "\"Source\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentDocuments_SchoolUserId_Source_ExternalId",
                table: "StudentDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Grades_SchoolUserId_Source_ExternalId",
                table: "Grades");

            migrationBuilder.DropIndex(
                name: "IX_AgendaEntries_SchoolUserId_Source_ExternalId",
                table: "AgendaEntries");

            migrationBuilder.DropIndex(
                name: "IX_Absences_SchoolUserId_Source_ExternalId",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "StudentDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StudentDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "SchoolUsers");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "AgendaEntries");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AgendaEntries");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Absences");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Absences");
        }
    }
}
