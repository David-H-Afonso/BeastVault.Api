using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTcgCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TcgSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderSetId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", nullable: true),
                    Series = table.Column<string>(type: "TEXT", nullable: true),
                    PrintedTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SymbolUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CardsSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TcgSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserApiCredentials",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ProtectedValue = table.Column<string>(type: "TEXT", nullable: false),
                    LastFour = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiCredentials", x => new { x.UserId, x.Provider });
                    table.ForeignKey(
                        name: "FK_UserApiCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TcgCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderCardId = table.Column<string>(type: "TEXT", nullable: false),
                    PokemonTcgIoId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", nullable: true),
                    Number = table.Column<string>(type: "TEXT", nullable: false),
                    Rarity = table.Column<string>(type: "TEXT", nullable: true),
                    Artist = table.Column<string>(type: "TEXT", nullable: true),
                    ImageSmall = table.Column<string>(type: "TEXT", nullable: true),
                    ImageLarge = table.Column<string>(type: "TEXT", nullable: true),
                    NationalPokedexNumbersJson = table.Column<string>(type: "TEXT", nullable: false),
                    VariantsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PriceEur = table.Column<decimal>(type: "TEXT", nullable: true),
                    PriceUsd = table.Column<decimal>(type: "TEXT", nullable: true),
                    VariantPricesEurJson = table.Column<string>(type: "TEXT", nullable: false),
                    VariantPricesUsdJson = table.Column<string>(type: "TEXT", nullable: false),
                    PriceUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CardmarketUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TcgplayerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetailedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TcgCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TcgCards_TcgSets_SetId",
                        column: x => x.SetId,
                        principalTable: "TcgSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTcgCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Variant = table.Column<string>(type: "TEXT", nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTcgCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTcgCards_TcgCards_CardId",
                        column: x => x.CardId,
                        principalTable: "TcgCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTcgCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TcgCards_Name",
                table: "TcgCards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TcgCards_Provider_ProviderCardId",
                table: "TcgCards",
                columns: new[] { "Provider", "ProviderCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TcgCards_SetId_Number",
                table: "TcgCards",
                columns: new[] { "SetId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_TcgSets_Provider_ProviderSetId",
                table: "TcgSets",
                columns: new[] { "Provider", "ProviderSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TcgSets_ReleaseDate",
                table: "TcgSets",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserTcgCards_CardId",
                table: "UserTcgCards",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTcgCards_UserId_AddedAt",
                table: "UserTcgCards",
                columns: new[] { "UserId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTcgCards_UserId_CardId_Variant_Condition_Language",
                table: "UserTcgCards",
                columns: new[] { "UserId", "CardId", "Variant", "Condition", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserApiCredentials");

            migrationBuilder.DropTable(
                name: "UserTcgCards");

            migrationBuilder.DropTable(
                name: "TcgCards");

            migrationBuilder.DropTable(
                name: "TcgSets");
        }
    }
}
