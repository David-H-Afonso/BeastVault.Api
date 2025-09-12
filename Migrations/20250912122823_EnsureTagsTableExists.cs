using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeastVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnsureTagsTableExists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure Tags table exists (idempotent creation)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Tags"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Tags"" PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""ImagePath"" TEXT NULL
                );
            ");

            // Ensure unique index exists on Tags.Name
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Tags_Name"" ON ""Tags"" (""Name"");
            ");

            // Ensure PokemonTags junction table exists
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PokemonTags"" (
                    ""PokemonId"" INTEGER NOT NULL,
                    ""TagId"" INTEGER NOT NULL,
                    CONSTRAINT ""PK_PokemonTags"" PRIMARY KEY (""PokemonId"", ""TagId""),
                    CONSTRAINT ""FK_PokemonTags_Pokemon_PokemonId"" FOREIGN KEY (""PokemonId"") REFERENCES ""Pokemon"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_PokemonTags_Tags_TagId"" FOREIGN KEY (""TagId"") REFERENCES ""Tags"" (""Id"") ON DELETE CASCADE
                );
            ");

            // Ensure indexes exist on PokemonTags
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_PokemonTags_TagId"" ON ""PokemonTags"" (""TagId"");
            ");

            // Ensure FileTags junction table exists
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""FileTags"" (
                    ""FileId"" INTEGER NOT NULL,
                    ""TagId"" INTEGER NOT NULL,
                    CONSTRAINT ""PK_FileTags"" PRIMARY KEY (""FileId"", ""TagId""),
                    CONSTRAINT ""FK_FileTags_Files_FileId"" FOREIGN KEY (""FileId"") REFERENCES ""Files"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_FileTags_Tags_TagId"" FOREIGN KEY (""TagId"") REFERENCES ""Tags"" (""Id"") ON DELETE CASCADE
                );
            ");

            // Ensure indexes exist on FileTags
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_FileTags_TagId"" ON ""FileTags"" (""TagId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the tables and indexes if rolling back
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_FileTags_TagId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""FileTags"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_PokemonTags_TagId"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""PokemonTags"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Tags_Name"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Tags"";");
        }
    }
}
