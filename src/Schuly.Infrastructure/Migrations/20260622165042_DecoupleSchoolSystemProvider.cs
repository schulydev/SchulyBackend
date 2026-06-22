using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleSchoolSystemProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchulwareApiBaseUrl",
                table: "SchoolSystems");

            migrationBuilder.AddColumn<string>(
                name: "PrivateAuthStrategy",
                table: "SchoolSystems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateAuthStrategy",
                table: "SchoolSystems");

            migrationBuilder.AddColumn<string>(
                name: "SchulwareApiBaseUrl",
                table: "SchoolSystems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
