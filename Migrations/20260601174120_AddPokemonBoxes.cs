using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPokemonBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowseLayout",
                table: "UserPreferences",
                type: "TEXT",
                nullable: false,
                defaultValue: "list");

            migrationBuilder.CreateTable(
                name: "PokemonBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokemonBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokemonBoxes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokemonBoxSlots",
                columns: table => new
                {
                    BoxId = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    PokemonId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokemonBoxSlots", x => new { x.BoxId, x.SlotIndex });
                    table.ForeignKey(
                        name: "FK_PokemonBoxSlots_PokemonBoxes_BoxId",
                        column: x => x.BoxId,
                        principalTable: "PokemonBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PokemonBoxSlots_Pokemon_PokemonId",
                        column: x => x.PokemonId,
                        principalTable: "Pokemon",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PokemonBoxes_UserId_SortOrder",
                table: "PokemonBoxes",
                columns: new[] { "UserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PokemonBoxSlots_PokemonId",
                table: "PokemonBoxSlots",
                column: "PokemonId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Moves_Pokemon_PokemonId",
                table: "Moves",
                column: "PokemonId",
                principalTable: "Pokemon",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RelearnMoves_Pokemon_PokemonId",
                table: "RelearnMoves",
                column: "PokemonId",
                principalTable: "Pokemon",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Moves_Pokemon_PokemonId",
                table: "Moves");

            migrationBuilder.DropForeignKey(
                name: "FK_RelearnMoves_Pokemon_PokemonId",
                table: "RelearnMoves");

            migrationBuilder.DropTable(
                name: "PokemonBoxSlots");

            migrationBuilder.DropTable(
                name: "PokemonBoxes");

            migrationBuilder.DropColumn(
                name: "BrowseLayout",
                table: "UserPreferences");
        }
    }
}
