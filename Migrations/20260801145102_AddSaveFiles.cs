using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSaveFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaveFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    StoredPath = table.Column<string>(type: "TEXT", nullable: false),
                    RawBlob = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Generation = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginGame = table.Column<int>(type: "INTEGER", nullable: false),
                    GameName = table.Column<string>(type: "TEXT", nullable: false),
                    SaveType = table.Column<string>(type: "TEXT", nullable: false),
                    ChecksumsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaveFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaveFiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavePokedexEntries",
                columns: table => new
                {
                    SaveFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesName = table.Column<string>(type: "TEXT", nullable: false),
                    Seen = table.Column<bool>(type: "INTEGER", nullable: false),
                    Caught = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavePokedexEntries", x => new { x.SaveFileId, x.SpeciesId });
                    table.ForeignKey(
                        name: "FK_SavePokedexEntries_SaveFiles_SaveFileId",
                        column: x => x.SaveFileId,
                        principalTable: "SaveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavePokemonPreviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaveFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesName = table.Column<string>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    IsShiny = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEgg = table.Column<bool>(type: "INTEGER", nullable: false),
                    Form = table.Column<int>(type: "INTEGER", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    Nature = table.Column<int>(type: "INTEGER", nullable: false),
                    NatureName = table.Column<string>(type: "TEXT", nullable: false),
                    AbilityName = table.Column<string>(type: "TEXT", nullable: false),
                    HeldItemName = table.Column<string>(type: "TEXT", nullable: false),
                    MovesJson = table.Column<string>(type: "TEXT", nullable: false),
                    PokemonHash = table.Column<string>(type: "TEXT", nullable: false),
                    PokemonStoredHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavePokemonPreviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavePokemonPreviews_SaveFiles_SaveFileId",
                        column: x => x.SaveFileId,
                        principalTable: "SaveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaveTrainers",
                columns: table => new
                {
                    SaveFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainerName = table.Column<string>(type: "TEXT", nullable: false),
                    TrainerId = table.Column<uint>(type: "INTEGER", nullable: false),
                    SecretId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Money = table.Column<uint>(type: "INTEGER", nullable: false),
                    PlayTimeHours = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    BadgeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DexSeen = table.Column<int>(type: "INTEGER", nullable: false),
                    DexCaught = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaveTrainers", x => x.SaveFileId);
                    table.ForeignKey(
                        name: "FK_SaveTrainers_SaveFiles_SaveFileId",
                        column: x => x.SaveFileId,
                        principalTable: "SaveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaveFiles_UserId_ImportedAt",
                table: "SaveFiles",
                columns: new[] { "UserId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SaveFiles_UserId_Sha256",
                table: "SaveFiles",
                columns: new[] { "UserId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavePokemonPreviews_PokemonHash",
                table: "SavePokemonPreviews",
                column: "PokemonHash");

            migrationBuilder.CreateIndex(
                name: "IX_SavePokemonPreviews_SaveFileId_Location_BoxIndex_SlotIndex",
                table: "SavePokemonPreviews",
                columns: new[] { "SaveFileId", "Location", "BoxIndex", "SlotIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavePokedexEntries");

            migrationBuilder.DropTable(
                name: "SavePokemonPreviews");

            migrationBuilder.DropTable(
                name: "SaveTrainers");

            migrationBuilder.DropTable(
                name: "SaveFiles");
        }
    }
}
