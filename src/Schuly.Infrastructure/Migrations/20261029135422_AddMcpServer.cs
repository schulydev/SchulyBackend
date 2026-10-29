using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schuly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    McpGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    McpApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Allowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpActivities_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Permissions = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpApiKeys_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpOAuthClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RedirectUris = table.Column<List<string>>(type: "text[]", nullable: false),
                    ClientUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SoftwareId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SoftwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpOAuthClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    McpOAuthClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<string>(type: "text", nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpGrants_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_McpGrants_McpOAuthClients_McpOAuthClientId",
                        column: x => x.McpOAuthClientId,
                        principalTable: "McpOAuthClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpAuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    McpGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CodeChallenge = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Resource = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpAuthorizationCodes_McpGrants_McpGrantId",
                        column: x => x.McpGrantId,
                        principalTable: "McpGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    McpGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    Resource = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpTokens_ApplicationUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "ApplicationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_McpTokens_McpGrants_McpGrantId",
                        column: x => x.McpGrantId,
                        principalTable: "McpGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpActivities_ApplicationUserId",
                table: "McpActivities",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpActivities_CreatedAt",
                table: "McpActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_McpActivities_McpApiKeyId_CreatedAt",
                table: "McpActivities",
                columns: new[] { "McpApiKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_McpActivities_McpGrantId_CreatedAt",
                table: "McpActivities",
                columns: new[] { "McpGrantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_McpApiKeys_ApplicationUserId",
                table: "McpApiKeys",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpApiKeys_KeyHash",
                table: "McpApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAuthorizationCodes_CodeHash",
                table: "McpAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAuthorizationCodes_ExpiresAt",
                table: "McpAuthorizationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_McpAuthorizationCodes_McpGrantId",
                table: "McpAuthorizationCodes",
                column: "McpGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_McpGrants_ApplicationUserId_McpOAuthClientId",
                table: "McpGrants",
                columns: new[] { "ApplicationUserId", "McpOAuthClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_McpGrants_McpOAuthClientId",
                table: "McpGrants",
                column: "McpOAuthClientId");

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthClients_ClientId",
                table: "McpOAuthClients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpOAuthClients_RegistrationIpHash_CreatedAt",
                table: "McpOAuthClients",
                columns: new[] { "RegistrationIpHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_McpTokens_ApplicationUserId",
                table: "McpTokens",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpTokens_ExpiresAt",
                table: "McpTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_McpTokens_McpGrantId",
                table: "McpTokens",
                column: "McpGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_McpTokens_TokenHash",
                table: "McpTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpActivities");

            migrationBuilder.DropTable(
                name: "McpApiKeys");

            migrationBuilder.DropTable(
                name: "McpAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "McpTokens");

            migrationBuilder.DropTable(
                name: "McpGrants");

            migrationBuilder.DropTable(
                name: "McpOAuthClients");
        }
    }
}
