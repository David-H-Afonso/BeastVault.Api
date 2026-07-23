using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    GrantedScopes = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdAccessTokens_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdAuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", nullable: false),
                    CodeChallenge = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdAuthorizationCodes_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdRefreshTokens_HouseholdConnections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "HouseholdConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdRefreshTokens_HouseholdRefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "HouseholdRefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_ConnectionId_FamilyId",
                table: "HouseholdAccessTokens",
                columns: new[] { "ConnectionId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_ExpiresAt",
                table: "HouseholdAccessTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessTokens_TokenHash",
                table: "HouseholdAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAuthorizationCodes_CodeHash",
                table: "HouseholdAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAuthorizationCodes_ConnectionId",
                table: "HouseholdAuthorizationCodes",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAuthorizationCodes_ExpiresAt",
                table: "HouseholdAuthorizationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdConnections_UserId_ClientId",
                table: "HouseholdConnections",
                columns: new[] { "UserId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_ConnectionId_FamilyId",
                table: "HouseholdRefreshTokens",
                columns: new[] { "ConnectionId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_ExpiresAt",
                table: "HouseholdRefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_ReplacedByTokenId",
                table: "HouseholdRefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRefreshTokens_TokenHash",
                table: "HouseholdRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdAccessTokens");

            migrationBuilder.DropTable(
                name: "HouseholdAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "HouseholdRefreshTokens");

            migrationBuilder.DropTable(
                name: "HouseholdConnections");
        }
    }
}
