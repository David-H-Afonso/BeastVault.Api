using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class BulbapediaNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntriesCount",
                table: "BulbapediaCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocationsCount",
                table: "BulbapediaCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameMeaning",
                table: "BulbapediaCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NormalizedAt",
                table: "BulbapediaCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedError",
                table: "BulbapediaCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NormalizedStatus",
                table: "BulbapediaCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawHtml",
                table: "BulbapediaCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpritesCount",
                table: "BulbapediaCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PokedexSpriteEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Generation = table.Column<int>(type: "INTEGER", nullable: false),
                    GameSlug = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayLabel = table.Column<string>(type: "TEXT", nullable: false),
                    NormalLocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    ShinyLocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    BackLocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    BackShinyLocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokedexSpriteEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PokedexSpriteEntries_PokemonId",
                table: "PokedexSpriteEntries",
                column: "PokemonId");

            migrationBuilder.CreateIndex(
                name: "IX_PokedexSpriteEntries_SpeciesId_GameSlug",
                table: "PokedexSpriteEntries",
                columns: new[] { "SpeciesId", "GameSlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PokedexSpriteEntries");

            migrationBuilder.DropColumn(
                name: "EntriesCount",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "LocationsCount",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "NameMeaning",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "NormalizedAt",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "NormalizedError",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "NormalizedStatus",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "RawHtml",
                table: "BulbapediaCache");

            migrationBuilder.DropColumn(
                name: "SpritesCount",
                table: "BulbapediaCache");
        }
    }
}
