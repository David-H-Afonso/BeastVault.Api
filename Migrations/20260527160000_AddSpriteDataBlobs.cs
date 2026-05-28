using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSpriteDataBlobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ArtworkData",
                table: "PokedexPokemon",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ShinyData",
                table: "PokedexPokemon",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SpriteData",
                table: "PokedexPokemon",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtworkData",
                table: "PokedexPokemon");

            migrationBuilder.DropColumn(
                name: "ShinyData",
                table: "PokedexPokemon");

            migrationBuilder.DropColumn(
                name: "SpriteData",
                table: "PokedexPokemon");
        }
    }
}
