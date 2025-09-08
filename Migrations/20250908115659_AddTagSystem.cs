using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTagSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Tags",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileTags",
                columns: table => new
                {
                    FileId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTags", x => new { x.FileId, x.TagId });
                    table.ForeignKey(
                        name: "FK_FileTags_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PokemonTags_TagId",
                table: "PokemonTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTags_TagId",
                table: "FileTags",
                column: "TagId");

            migrationBuilder.AddForeignKey(
                name: "FK_PokemonTags_Pokemon_PokemonId",
                table: "PokemonTags",
                column: "PokemonId",
                principalTable: "Pokemon",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PokemonTags_Tags_TagId",
                table: "PokemonTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PokemonTags_Pokemon_PokemonId",
                table: "PokemonTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PokemonTags_Tags_TagId",
                table: "PokemonTags");

            migrationBuilder.DropTable(
                name: "FileTags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_PokemonTags_TagId",
                table: "PokemonTags");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Tags");
        }
    }
}
