using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSpriteCacheLocalPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtworkLocalPath",
                table: "PokedexPokemon",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpriteLocalPath",
                table: "PokedexPokemon",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpriteLocalPath",
                table: "PokedexItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtworkLocalPath",
                table: "PokedexPokemon");

            migrationBuilder.DropColumn(
                name: "SpriteLocalPath",
                table: "PokedexPokemon");

            migrationBuilder.DropColumn(
                name: "SpriteLocalPath",
                table: "PokedexItems");
        }
    }
}
