using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndMultiUserSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Users table first
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            // 2. Seed default admin user (Id=1) before adding FK columns
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Username", "PasswordHash", "Role", "IsDefault", "CreatedAt" },
                values: new object[] { "Admin", null!, 1, true, DateTime.UtcNow });

            // 3. Drop old unique indexes
            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Files_Sha256",
                table: "Files");

            // 4. Add UserId columns with default = 1 (admin user)
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Tags",
                type: "INTEGER",
                nullable: true);

            // Assign existing tags to admin
            migrationBuilder.Sql("UPDATE Tags SET UserId = 1;");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Pokemon",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UserId",
                table: "Tags",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UserId_Name",
                table: "Tags",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pokemon_UserId",
                table: "Pokemon",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_UserId",
                table: "Files",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_UserId_Sha256",
                table: "Files",
                columns: new[] { "UserId", "Sha256" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_UserId",
                table: "Files",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pokemon_Users_UserId",
                table: "Pokemon",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Users_UserId",
                table: "Tags",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_UserId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Pokemon_Users_UserId",
                table: "Pokemon");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Users_UserId",
                table: "Tags");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tags_UserId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_UserId_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Pokemon_UserId",
                table: "Pokemon");

            migrationBuilder.DropIndex(
                name: "IX_Files_UserId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_UserId_Sha256",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Pokemon");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Files");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_Sha256",
                table: "Files",
                column: "Sha256",
                unique: true);
        }
    }
}
