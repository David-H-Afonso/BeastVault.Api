using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class RefineSavesAndTcg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficialCode",
                table: "TcgSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesId",
                table: "TcgSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRefreshError",
                table: "TcgCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceCheckedAt",
                table: "TcgCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "SaveFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TcgSets_OfficialCode",
                table: "TcgSets",
                column: "OfficialCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TcgSets_OfficialCode",
                table: "TcgSets");

            migrationBuilder.DropColumn(
                name: "OfficialCode",
                table: "TcgSets");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "TcgSets");

            migrationBuilder.DropColumn(
                name: "LastRefreshError",
                table: "TcgCards");

            migrationBuilder.DropColumn(
                name: "PriceCheckedAt",
                table: "TcgCards");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "SaveFiles");
        }
    }
}
