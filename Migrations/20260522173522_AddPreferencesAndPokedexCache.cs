using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferencesAndPokedexCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PokedexEntries",
                columns: table => new
                {
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LocalizedNames = table.Column<string>(type: "TEXT", nullable: false),
                    Genus = table.Column<string>(type: "TEXT", nullable: false),
                    FlavorText = table.Column<string>(type: "TEXT", nullable: false),
                    Generation = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    Shape = table.Column<string>(type: "TEXT", nullable: false),
                    Habitat = table.Column<string>(type: "TEXT", nullable: false),
                    GrowthRate = table.Column<string>(type: "TEXT", nullable: false),
                    CaptureRate = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseHappiness = table.Column<int>(type: "INTEGER", nullable: false),
                    HatchCounter = table.Column<int>(type: "INTEGER", nullable: false),
                    GenderRate = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLegendary = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMythical = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBaby = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasGenderDifferences = table.Column<bool>(type: "INTEGER", nullable: false),
                    FormsSwitchable = table.Column<bool>(type: "INTEGER", nullable: false),
                    EggGroups = table.Column<string>(type: "TEXT", nullable: false),
                    Varieties = table.Column<string>(type: "TEXT", nullable: false),
                    EvolutionChainUrl = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokedexEntries", x => x.SpeciesId);
                });

            migrationBuilder.CreateTable(
                name: "PokedexPokemon",
                columns: table => new
                {
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseExperience = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Types = table.Column<string>(type: "TEXT", nullable: false),
                    Abilities = table.Column<string>(type: "TEXT", nullable: false),
                    BaseStats = table.Column<string>(type: "TEXT", nullable: false),
                    Sprites = table.Column<string>(type: "TEXT", nullable: false),
                    Cries = table.Column<string>(type: "TEXT", nullable: false),
                    GameIndices = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokedexPokemon", x => x.PokemonId);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    ViewMode = table.Column<string>(type: "TEXT", nullable: false),
                    SpriteType = table.Column<string>(type: "TEXT", nullable: false),
                    BackgroundType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PokedexPokemon_SpeciesId",
                table: "PokedexPokemon",
                column: "SpeciesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PokedexEntries");

            migrationBuilder.DropTable(
                name: "PokedexPokemon");

            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
