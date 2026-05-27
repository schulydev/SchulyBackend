using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgendaEntryEndDateAndHolidayType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "AgendaEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AgendaEntry_EndDateAfterDate",
                table: "AgendaEntries",
                sql: "\"EndDate\" IS NULL OR \"EndDate\" >= \"Date\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AgendaEntry_EndDateAfterDate",
                table: "AgendaEntries");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "AgendaEntries");
        }
    }
}
