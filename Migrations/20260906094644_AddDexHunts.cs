using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDexHunts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DexHuntLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DexHuntLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DexHuntLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DexHuntItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HuntListId = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCaught = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CaughtAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DexHuntItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DexHuntItems_DexHuntLists_HuntListId",
                        column: x => x.HuntListId,
                        principalTable: "DexHuntLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DexHuntItems_HuntListId_IsCaught",
                table: "DexHuntItems",
                columns: new[] { "HuntListId", "IsCaught" });

            migrationBuilder.CreateIndex(
                name: "IX_DexHuntItems_HuntListId_SortOrder",
                table: "DexHuntItems",
                columns: new[] { "HuntListId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DexHuntItems_HuntListId_SpeciesId",
                table: "DexHuntItems",
                columns: new[] { "HuntListId", "SpeciesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DexHuntLists_UserId_Name",
                table: "DexHuntLists",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DexHuntLists_UserId_SortOrder",
                table: "DexHuntLists",
                columns: new[] { "UserId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DexHuntItems");

            migrationBuilder.DropTable(
                name: "DexHuntLists");
        }
    }
}
