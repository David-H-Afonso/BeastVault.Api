using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    public partial class SyncSchemaWithMetLevel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All changes already applied to DB manually.
            // This migration only syncs the EF model snapshot.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
