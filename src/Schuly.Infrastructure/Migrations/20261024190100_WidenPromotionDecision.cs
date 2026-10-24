using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenPromotionDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PromotionDecision",
                table: "SemesterReports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PromotionDecision",
                table: "SemesterReports",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
