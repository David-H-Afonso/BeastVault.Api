using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTagImageAndFileNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Pokemon_FileId",
                table: "Pokemon",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pokemon_Files_FileId",
                table: "Pokemon",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pokemon_Files_FileId",
                table: "Pokemon");

            migrationBuilder.DropIndex(
                name: "IX_Pokemon_FileId",
                table: "Pokemon");
        }
    }
}
