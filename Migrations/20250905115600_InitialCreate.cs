using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: true),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    StoredPath = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawBlob = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moves",
                columns: table => new
                {
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    MoveId = table.Column<int>(type: "INTEGER", nullable: false),
                    PpUps = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPp = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moves", x => new { x.PokemonId, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "Pokemon",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    OtName = table.Column<string>(type: "TEXT", nullable: false),
                    Tid = table.Column<int>(type: "INTEGER", nullable: false),
                    Sid = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    IsShiny = table.Column<bool>(type: "INTEGER", nullable: false),
                    Nature = table.Column<int>(type: "INTEGER", nullable: false),
                    AbilityId = table.Column<int>(type: "INTEGER", nullable: false),
                    BallId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeraType = table.Column<int>(type: "INTEGER", nullable: true),
                    HeldItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginGame = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    MetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MetLocation = table.Column<string>(type: "TEXT", nullable: true),
                    SpriteKey = table.Column<string>(type: "TEXT", nullable: false),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    OTGender = table.Column<int>(type: "INTEGER", nullable: false),
                    OTLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptionConstant = table.Column<uint>(type: "INTEGER", nullable: false),
                    PersonalityId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Experience = table.Column<uint>(type: "INTEGER", nullable: false),
                    CurrentFriendship = table.Column<int>(type: "INTEGER", nullable: false),
                    Form = table.Column<int>(type: "INTEGER", nullable: false),
                    FormArgument = table.Column<uint>(type: "INTEGER", nullable: false),
                    DynamaxLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CanGigantamax = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEgg = table.Column<bool>(type: "INTEGER", nullable: false),
                    FatefulEncounter = table.Column<bool>(type: "INTEGER", nullable: false),
                    EggLocation = table.Column<int>(type: "INTEGER", nullable: false),
                    EggMetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HeightScalar = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightScalar = table.Column<int>(type: "INTEGER", nullable: false),
                    Scale = table.Column<int>(type: "INTEGER", nullable: false),
                    PokerusState = table.Column<int>(type: "INTEGER", nullable: false),
                    PokerusDays = table.Column<int>(type: "INTEGER", nullable: false),
                    PokerusStrain = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestCool = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestBeauty = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestCute = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestSmart = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestTough = table.Column<int>(type: "INTEGER", nullable: false),
                    ContestSheen = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHandler = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerName = table.Column<string>(type: "TEXT", nullable: false),
                    HandlingTrainerGender = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerLanguage = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerFriendship = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTrainerMemory = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTrainerMemoryIntensity = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTrainerMemoryFeeling = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTrainerMemoryVariable = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerMemory = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerMemoryIntensity = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerMemoryFeeling = table.Column<int>(type: "INTEGER", nullable: false),
                    HandlingTrainerMemoryVariable = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pokemon", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PokemonTags",
                columns: table => new
                {
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokemonTags", x => new { x.PokemonId, x.TagId });
                });

            migrationBuilder.CreateTable(
                name: "RelearnMoves",
                columns: table => new
                {
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    MoveId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelearnMoves", x => new { x.PokemonId, x.Slot });
                });

            migrationBuilder.CreateTable(
                name: "Stats",
                columns: table => new
                {
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IvHp = table.Column<int>(type: "INTEGER", nullable: false),
                    IvAtk = table.Column<int>(type: "INTEGER", nullable: false),
                    IvDef = table.Column<int>(type: "INTEGER", nullable: false),
                    IvSpa = table.Column<int>(type: "INTEGER", nullable: false),
                    IvSpd = table.Column<int>(type: "INTEGER", nullable: false),
                    IvSpe = table.Column<int>(type: "INTEGER", nullable: false),
                    EvHp = table.Column<int>(type: "INTEGER", nullable: false),
                    EvAtk = table.Column<int>(type: "INTEGER", nullable: false),
                    EvDef = table.Column<int>(type: "INTEGER", nullable: false),
                    EvSpa = table.Column<int>(type: "INTEGER", nullable: false),
                    EvSpd = table.Column<int>(type: "INTEGER", nullable: false),
                    EvSpe = table.Column<int>(type: "INTEGER", nullable: false),
                    HyperTrainedHp = table.Column<bool>(type: "INTEGER", nullable: false),
                    HyperTrainedAtk = table.Column<bool>(type: "INTEGER", nullable: false),
                    HyperTrainedDef = table.Column<bool>(type: "INTEGER", nullable: false),
                    HyperTrainedSpa = table.Column<bool>(type: "INTEGER", nullable: false),
                    HyperTrainedSpd = table.Column<bool>(type: "INTEGER", nullable: false),
                    HyperTrainedSpe = table.Column<bool>(type: "INTEGER", nullable: false),
                    StatHp = table.Column<int>(type: "INTEGER", nullable: false),
                    StatAtk = table.Column<int>(type: "INTEGER", nullable: false),
                    StatDef = table.Column<int>(type: "INTEGER", nullable: false),
                    StatSpa = table.Column<int>(type: "INTEGER", nullable: false),
                    StatSpd = table.Column<int>(type: "INTEGER", nullable: false),
                    StatSpe = table.Column<int>(type: "INTEGER", nullable: false),
                    StatHpCurrent = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stats", x => x.PokemonId);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_Sha256",
                table: "Files",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pokemon_OriginGame",
                table: "Pokemon",
                column: "OriginGame");

            migrationBuilder.CreateIndex(
                name: "IX_Pokemon_SpeciesId_IsShiny",
                table: "Pokemon",
                columns: new[] { "SpeciesId", "IsShiny" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Moves");

            migrationBuilder.DropTable(
                name: "Pokemon");

            migrationBuilder.DropTable(
                name: "PokemonTags");

            migrationBuilder.DropTable(
                name: "RelearnMoves");

            migrationBuilder.DropTable(
                name: "Stats");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
