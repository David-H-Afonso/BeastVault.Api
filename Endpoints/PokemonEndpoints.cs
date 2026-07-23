
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Helpers;
using BeastVault.Api.Application.Interfaces;


namespace BeastVault.Api.Endpoints
{
    public static class PokemonEndpoints
    {
        public static IEndpointRouteBuilder MapPokemonEndpoints(this IEndpointRouteBuilder app)
        {
            // Admin endpoint to wipe the entire database (dangerous!)
            app.MapPost("/admin/wipe-database", async (AppDbContext db, FileStorageService storage) =>
            {
                // Get all files to delete their backups
                var allFiles = await db.Files.ToListAsync();

                // Remove all data from database
                db.Pokemon.RemoveRange(db.Pokemon);
                db.PokemonTags.RemoveRange(db.PokemonTags);
                db.FileTags.RemoveRange(db.FileTags);
                db.Stats.RemoveRange(db.Stats);
                db.Moves.RemoveRange(db.Moves);
                db.RelearnMoves.RemoveRange(db.RelearnMoves);
                db.Files.RemoveRange(db.Files);
                db.Tags.RemoveRange(db.Tags);
                await db.SaveChangesAsync();

                // Delete all backup files
                int deletedBackups = 0;
                foreach (var file in allFiles)
                {
                    if (!string.IsNullOrEmpty(file.OriginalFileName))
                    {
                        try
                        {
                            var ext = Path.GetExtension(file.OriginalFileName);
                            storage.DeleteBackup(file.OriginalFileName, ext, file.UserId);
                            deletedBackups++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not delete backup for {file.OriginalFileName}: {ex.Message}");
                        }
                    }
                }

                return Results.Ok(new { Message = "Database wiped.", DeletedBackups = deletedBackups });
            })
            .WithName("WipeDatabase")
            .WithSummary("⚠️ ADMIN: Delete entire database")
            .WithDescription("DANGEROUS: Removes all Pokémon, files and data from the database. For development/testing only.")
            .WithTags("Admin")
            .Produces<string>(200)
            .RequireAuthorization("AdminPolicy");
            // Eliminar de la base de datos y archivo principal (conserva backup)
            app.MapDelete("/pokemon/{pokemonId:int}/database", async (int pokemonId, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var (success, fileDeleted, backupPreserved) = await pokemonService.DeletePokemonDatabaseAsync(userId.Value, pokemonId);
                if (!success) return Results.NotFound();

                return Results.Ok(new { Deleted = true, FileDeleted = fileDeleted, BackupPreserved = backupPreserved });
            })
            .WithName("DeletePokemonFromDatabase")
            .WithSummary("Delete a Pokémon and its main file (preserves backup)")
            .WithDescription("Removes the Pokémon, all its related data from the database, and deletes the main file on disk. Backup file is preserved.")
            .WithTags("Pokemon", "Admin")
            .Produces<object>(200)
            .Produces(404)
            .RequireAuthorization();

            // Eliminar de base de datos y backup/disco
            app.MapDelete("/pokemon/{pokemonId:int}/backup", async (int pokemonId, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var (success, fileDeleted, backupDeleted, fileName) = await pokemonService.DeletePokemonAndBackupAsync(userId.Value, pokemonId);
                if (!success) return Results.NotFound();

                return Results.Ok(new { Deleted = true, FileDeleted = fileDeleted, BackupDeleted = backupDeleted, FileName = fileName });
            })
            .WithName("DeletePokemonAndBackup")
            .WithSummary("Delete a Pokémon completely (database + file)")
            .WithDescription("Removes the Pokémon, all its related data and the original file from disk. Irreversible operation.")
            .WithTags("Pokemon", "Admin")
            .Produces<object>(200)
            .Produces(404)
            .RequireAuthorization();

            // Main Pokemon query endpoint with advanced filtering, sorting and pagination
            app.MapGet("/pokemon", async (IPokemonService pokemonService, [AsParameters] AdvancedPokemonQuery q, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var result = await pokemonService.GetPokemonListAsync(userId.Value, q);
                if (!ctx.IsHouseholdIntegration()) return Results.Ok(result);

                var integrationResult = new HouseholdPokemonListResponseDto(
                    result.Items.Select(item => new HouseholdPokemonListItemDto(
                        item.Id,
                        item.SpeciesId,
                        item.SpeciesName,
                        item.Nickname,
                        item.Level,
                        item.IsShiny,
                        item.Favorite,
                        item.IsEgg,
                        item.Type1,
                        item.Type2,
                        BuildHouseholdSpriteUrl(item),
                        item.Tags.Select(tag => new HouseholdPokemonTagDto(
                            tag.Id,
                            tag.Name,
                            tag.ImagePath,
                            tag.ColorHex)).ToList())).ToList(),
                    result.Total);
                return Results.Ok(integrationResult);
            })
            .WithName("GetPokemonList")
            .WithSummary("Get Pokemon with advanced filtering, sorting and pagination")
            .WithDescription("Main endpoint with comprehensive filtering by types, generations, stats, and flexible sorting options.")
            .WithTags("Pokemon")
            .Produces<object>(200)
            .RequireAuthorization("PokemonReadPolicy");

            app.MapGet("/pokemon/summary", async (IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var summary = await pokemonService.GetPokemonSummaryAsync(userId.Value);
                return ctx.IsHouseholdIntegration()
                    ? Results.Ok(new HouseholdPokemonSummaryDto(summary.Counts))
                    : Results.Ok(summary);
            })
            .WithName("GetPokemonSummary")
            .WithSummary("Get ownership-scoped Pokémon counts, recent imports and tags")
            .WithTags("Pokemon")
            .Produces<PokemonSummaryDto>(200)
            .RequireAuthorization("PokemonReadPolicy");

            // Per-tag match counts for the current search/filter context (faceted counts).
            // Tag include/exclude filters are ignored so each tag shows how many of the
            // current (search + non-tag filtered) matches belong to it.
            app.MapGet("/pokemon/tag-counts", async (IPokemonService pokemonService, [AsParameters] AdvancedPokemonQuery q, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var result = await pokemonService.GetTagFacetCountsAsync(userId.Value, q);
                return Results.Ok(result);
            })
            .WithName("GetPokemonTagCounts")
            .WithSummary("Get per-tag match counts for the current search/filters")
            .WithDescription("Returns the total matches (ignoring tag selection) and a tagId→count map so tag tabs can reflect the active search and filters.")
            .WithTags("Pokemon", "Tags")
            .Produces<TagFacetCountsDto>(200)
            .RequireAuthorization();

            // Metadata endpoint for frontend helpers
            app.MapGet("/pokemon/metadata", () =>
            {
                var types = new[]
                {
                    new { Id = 0, Name = "Normal" },
                    new { Id = 1, Name = "Fighting" },
                    new { Id = 2, Name = "Flying" },
                    new { Id = 3, Name = "Poison" },
                    new { Id = 4, Name = "Ground" },
                    new { Id = 5, Name = "Rock" },
                    new { Id = 6, Name = "Bug" },
                    new { Id = 7, Name = "Ghost" },
                    new { Id = 8, Name = "Steel" },
                    new { Id = 9, Name = "Fire" },
                    new { Id = 10, Name = "Water" },
                    new { Id = 11, Name = "Grass" },
                    new { Id = 12, Name = "Electric" },
                    new { Id = 13, Name = "Psychic" },
                    new { Id = 14, Name = "Ice" },
                    new { Id = 15, Name = "Dragon" },
                    new { Id = 16, Name = "Dark" },
                    new { Id = 17, Name = "Fairy" }
                };
                var generations = Enumerable.Range(1, 9).Select(g => new { Id = g, Name = $"Generation {g}" }).ToList();
                var pokeballs = new[]
                {
                    new { Id = 0, Name = "Poké Ball" },
                    new { Id = 1, Name = "Master Ball" },
                    new { Id = 2, Name = "Ultra Ball" },
                    new { Id = 3, Name = "Great Ball" },
                    new { Id = 4, Name = "Poké Ball" },
                    new { Id = 5, Name = "Safari Ball" },
                    new { Id = 6, Name = "Net Ball" },
                    new { Id = 7, Name = "Dive Ball" },
                    new { Id = 8, Name = "Nest Ball" },
                    new { Id = 9, Name = "Repeat Ball" },
                    new { Id = 10, Name = "Timer Ball" },
                    new { Id = 11, Name = "Luxury Ball" },
                    new { Id = 12, Name = "Premier Ball" },
                    new { Id = 13, Name = "Dusk Ball" },
                    new { Id = 14, Name = "Heal Ball" },
                    new { Id = 15, Name = "Quick Ball" },
                    new { Id = 16, Name = "Cherish Ball" },
                    new { Id = 17, Name = "Fast Ball" },
                    new { Id = 18, Name = "Level Ball" },
                    new { Id = 19, Name = "Lure Ball" },
                    new { Id = 20, Name = "Heavy Ball" },
                    new { Id = 21, Name = "Love Ball" },
                    new { Id = 22, Name = "Friend Ball" },
                    new { Id = 23, Name = "Moon Ball" },
                    new { Id = 24, Name = "Sport Ball" },
                    new { Id = 25, Name = "Dream Ball" },
                    new { Id = 26, Name = "Beast Ball" },
                    new { Id = 27, Name = "Strange Ball" },
                    new { Id = 28, Name = "Feather Ball" },
                    new { Id = 29, Name = "Wing Ball" },
                    new { Id = 30, Name = "Jet Ball" },
                    new { Id = 31, Name = "Lead(en) Ball" },
                    new { Id = 32, Name = "Gigaton Ball" },
                    new { Id = 33, Name = "Origin Ball" }
                };

                // Temporarily disabled filters (not working properly):
                // - Gender filter
                // - Form filter  
                // - Held item filter
                /*
                var genders = new[]
                {
                    new { Id = 0, Name = "Unknown" },
                    new { Id = 1, Name = "Male" },
                    new { Id = 2, Name = "Female" }
                };
                */

                // Only working sort fields (disabled problematic ones)
                var workingSortFields = new[]
                {
                    new { Name = "Id", Value = (int)PokemonSortField.Id },
                    new { Name = "PokedexNumber", Value = (int)PokemonSortField.PokedexNumber },
                    new { Name = "Nickname", Value = (int)PokemonSortField.Nickname },
                    new { Name = "Level", Value = (int)PokemonSortField.Level },
                    new { Name = "Pokeball", Value = (int)PokemonSortField.Pokeball }
                };

                // Temporarily disabled sort fields (not working properly):
                // - SpeciesName: Requires PKHeX species resolution
                // - OriginGeneration: Complex generation mapping issues
                // - CapturedGeneration: Complex generation mapping issues  
                // - Gender: Database type conversion issues
                // - IsShiny: Boolean to int conversion issues
                // - Form: Field mapping issues
                // - CreatedAt: No actual CreatedAt field in database
                // - Favorite: Boolean to int conversion issues

                var typeFilterModes = Enum.GetValues<TypeFilterMode>()
                    .Select(m => new { Name = m.ToString(), Value = (int)m })
                    .ToList();

                return Results.Ok(new
                {
                    Types = types,
                    Pokeballs = pokeballs,
                    Generations = generations,
                    OriginGenerations = generations,
                    CapturedGenerations = generations,
                    // Genders = genders, // Temporarily disabled
                    SortFields = workingSortFields, // Only working sort fields
                    TypeFilterModes = typeFilterModes,
                    DefaultPageSize = 50,
                    MaxPageSize = 500
                });
            })
            .WithName("GetPokemonMetadata")
            .WithSummary("Get metadata for Pokemon filtering and sorting")
            .WithDescription("Returns available options for types, generations, sort fields, and other filter metadata.")
            .WithTags("Pokemon", "Metadata")
            .Produces<object>(200)
            .RequireAuthorization();

            app.MapGet("/pokemon/{id:int}", async (int id, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var detail = await pokemonService.GetPokemonByIdAsync(userId.Value, id);
                if (detail is null) return Results.NotFound();
                if (!ctx.IsHouseholdIntegration()) return Results.Ok(detail);

                return Results.Ok(new HouseholdPokemonDetailDto(
                    detail.Id,
                    detail.SpeciesId,
                    detail.SpeciesName,
                    detail.Form,
                    detail.FormName,
                    detail.Nickname,
                    detail.Level,
                    detail.IsShiny,
                    detail.IsEgg,
                    detail.Favorite,
                    detail.Notes,
                    detail.NatureName,
                    detail.AbilityName,
                    detail.BallName,
                    detail.GenderName,
                    detail.OriginGameName,
                    detail.MetLevel));
            })
            .WithName("GetPokemonById")
            .WithSummary("Get complete details of a Pokémon")
            .WithDescription("Returns all data of a specific Pokémon including stats, moves and relearn moves.")
            .WithTags("Pokemon")
            .Produces<PokemonDetailDto>(200)
            .Produces(404)
            .RequireAuthorization("PokemonReadPolicy");

            app.MapGet("/pokemon/{id:int}/showdown", async (int id, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var text = await pokemonService.GetShowdownExportAsync(userId.Value, id);
                return text is not null ? Results.Text(text) : Results.NotFound();
            })
            .WithName("ExportPokemonShowdown")
            .WithSummary("Export a Pokémon in Pokémon Showdown format")
            .WithDescription("Generates a Pokémon Showdown set with all the Pokémon data (moves, stats, item, etc.).")
            .WithTags("Pokemon")
            .Produces<string>(200, "text/plain")
            .Produces(404)
            .RequireAuthorization();

            app.MapPatch("/pokemon/{id:int}", async (int id, UpdatePokemonDto dto, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var updated = await pokemonService.UpdatePokemonAsync(userId.Value, id, dto);
                return updated ? Results.NoContent() : Results.NotFound();
            })
            .WithName("UpdatePokemon")
            .WithSummary("Update Pokémon properties")
            .WithDescription("Allows updating editable fields like favorite and notes. Only provided fields in the DTO are updated.")
            .WithTags("Pokemon")
            .Accepts<UpdatePokemonDto>("application/json")
            .Produces(204)
            .Produces(404)
            .RequireAuthorization();

            app.MapPatch("/pokemon/{id:int}/favorite", async (
                int id,
                HouseholdFavoriteRequest dto,
                IPokemonService pokemonService,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var updated = await pokemonService.UpdateFavoriteAsync(userId.Value, id, dto.Favorite);
                return updated ? Results.NoContent() : Results.NotFound();
            })
            .WithName("UpdatePokemonFavorite")
            .WithSummary("Update only a Pokémon favorite flag")
            .WithTags("Pokemon")
            .Produces(204)
            .Produces(404)
            .RequireAuthorization("PokemonFavoriteWritePolicy");

            app.MapPatch("/pokemon/{id:int}/notes", async (
                int id,
                HouseholdNotesRequest dto,
                IPokemonService pokemonService,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();
                if (dto.Notes is { Length: > 10_000 })
                    return Results.BadRequest(new { message = "Notes cannot exceed 10000 characters." });

                var updated = await pokemonService.UpdateNotesAsync(userId.Value, id, dto.Notes);
                return updated ? Results.NoContent() : Results.NotFound();
            })
            .WithName("UpdatePokemonNotes")
            .WithSummary("Update or clear only a Pokémon note")
            .WithTags("Pokemon")
            .Produces(204)
            .Produces(400)
            .Produces(404)
            .RequireAuthorization("PokemonNotesWritePolicy");

            // Compare two Pokemon to see differences (useful for debugging trades)
            app.MapGet("/pokemon/compare/{id1:int}/{id2:int}", async (int id1, int id2, IPokemonService pokemonService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var result = await pokemonService.ComparePokemonAsync(userId.Value, id1, id2);
                return result is not null ? Results.Ok(result) : Results.NotFound("One or both Pokemon not found");
            })
            .WithName("ComparePokemon")
            .WithSummary("Compare two Pokémon and show differences")
            .WithDescription("Analyzes and compares all fields of two different Pokémon. Useful for detecting changes after trades or edits.")
            .WithTags("Pokemon", "Comparison")
            .Produces<object>(200)
            .Produces(404)
            .RequireAuthorization();

            // Debug endpoint to check OriginGame values
            app.MapGet("/debug/origin-games", async (AppDbContext db) =>
            {
                var uniqueOriginGames = await db.Pokemon
                    .Select(p => new { p.OriginGame, p.SpeciesId })
                    .Distinct()
                    .OrderBy(x => x.OriginGame)
                    .Take(20)
                    .ToListAsync();

                var results = uniqueOriginGames.Select(x => new
                {
                    OriginGame = x.OriginGame,
                    SpeciesId = x.SpeciesId,
                    CalculatedGeneration = PokemonGameInfoService.GetGameGeneration(x.OriginGame),
                    SpeciesOriginGeneration = PokemonGameInfoService.GetSpeciesOriginGeneration(x.SpeciesId)
                });

                return Results.Ok(results);
            })
            .WithName("DebugOriginGames")
            .WithTags("Debug")
            .RequireAuthorization("AdminPolicy");

            return app;
        }

        private static string BuildHouseholdSpriteUrl(PokemonListItemDto item)
        {
            var sprite = item.IsShiny ? item.Sprites?.HomeShiny : item.Sprites?.Home;
            if (!string.IsNullOrWhiteSpace(sprite)) return sprite;

            var fallback = PokemonSpritesDto.ForPokemonId(item.SpeciesId);
            return item.IsShiny ? fallback.HomeShiny : fallback.Home;
        }
    }
}
