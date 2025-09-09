
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Domain.ValueObjects;


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
                            storage.DeleteBackup(file.OriginalFileName, ext);
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
            .Produces<string>(200);
            // Eliminar de la base de datos y archivo principal (conserva backup)
            app.MapDelete("/pokemon/{pokemonId:int}/database", async (int pokemonId, AppDbContext db, FileStorageService storage) =>
            {
                var poke = await db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId);
                if (poke == null) return Results.NotFound();

                // Obtener el archivo asociado antes de eliminar
                var file = await db.Files.FirstOrDefaultAsync(f => f.Id == poke.FileId);

                // Eliminar datos relacionados
                var stats = await db.Stats.Where(s => s.PokemonId == pokemonId).ToListAsync();
                db.Stats.RemoveRange(stats);
                var moves = await db.Moves.Where(m => m.PokemonId == pokemonId).ToListAsync();
                db.Moves.RemoveRange(moves);
                var relearnMoves = await db.RelearnMoves.Where(rm => rm.PokemonId == pokemonId).ToListAsync();
                db.RelearnMoves.RemoveRange(relearnMoves);

                // Eliminar Pokemon
                db.Pokemon.Remove(poke);

                // Eliminar archivo físico principal si existe (preserva backup)
                bool fileDeleted = false;
                if (file != null)
                {
                    Console.WriteLine($"Attempting to delete main file: {file.FileName}");
                    Console.WriteLine($"Stored path: {file.StoredPath}");
                    Console.WriteLine($"File exists: {File.Exists(file.StoredPath)}");

                    try
                    {
                        storage.Delete(file.StoredPath);
                        fileDeleted = true;
                        Console.WriteLine($"Main file deleted successfully: {file.StoredPath} (backup preserved)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete main file {file.StoredPath}: {ex.Message}");
                        Console.WriteLine($"Exception details: {ex}");
                    }

                    db.Files.Remove(file);
                }
                else
                {
                    Console.WriteLine("No file record found for this Pokemon");
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { Deleted = true, FileDeleted = fileDeleted, BackupPreserved = true });
            })
            .WithName("DeletePokemonFromDatabase")
            .WithSummary("Delete a Pokémon and its main file (preserves backup)")
            .WithDescription("Removes the Pokémon, all its related data from the database, and deletes the main file on disk. Backup file is preserved.")
            .WithTags("Pokemon", "Admin")
            .Produces<object>(200)
            .Produces(404);

            // Eliminar de base de datos y backup/disco
            app.MapDelete("/pokemon/{pokemonId:int}/backup", async (int pokemonId, AppDbContext db, FileStorageService storage) =>
            {
                var poke = await db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId);
                if (poke == null) return Results.NotFound();

                // Obtener el archivo asociado antes de eliminar
                var file = await db.Files.FirstOrDefaultAsync(f => f.Id == poke.FileId);

                // Eliminar datos relacionados
                var stats = await db.Stats.Where(s => s.PokemonId == pokemonId).ToListAsync();
                db.Stats.RemoveRange(stats);
                var moves = await db.Moves.Where(m => m.PokemonId == pokemonId).ToListAsync();
                db.Moves.RemoveRange(moves);
                var relearnMoves = await db.RelearnMoves.Where(rm => rm.PokemonId == pokemonId).ToListAsync();
                db.RelearnMoves.RemoveRange(relearnMoves);

                // Eliminar Pokemon
                db.Pokemon.Remove(poke);

                // Eliminar archivo físico principal si existe
                bool fileDeleted = false;
                bool backupDeleted = false;
                if (file != null)
                {
                    Console.WriteLine($"Attempting to delete file: {file.FileName}");
                    Console.WriteLine($"Stored path: {file.StoredPath}");
                    Console.WriteLine($"File exists: {File.Exists(file.StoredPath)}");

                    try
                    {
                        storage.Delete(file.StoredPath);
                        fileDeleted = true;
                        Console.WriteLine($"Physical file deleted successfully: {file.StoredPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete physical file {file.StoredPath}: {ex.Message}");
                        Console.WriteLine($"Exception details: {ex}");
                    }

                    // Eliminar backup si existe
                    if (!string.IsNullOrEmpty(file.OriginalFileName))
                    {
                        try
                        {
                            var ext = Path.GetExtension(file.OriginalFileName);
                            storage.DeleteBackup(file.OriginalFileName, ext);
                            backupDeleted = true;
                            Console.WriteLine($"Backup file deleted successfully: {file.OriginalFileName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not delete backup file {file.OriginalFileName}: {ex.Message}");
                        }
                    }

                    db.Files.Remove(file);
                }
                else
                {
                    Console.WriteLine("No file record found for this Pokemon");
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { Deleted = true, FileDeleted = fileDeleted, BackupDeleted = backupDeleted, FileName = file?.FileName });
            })
            .WithName("DeletePokemonAndBackup")
            .WithSummary("Delete a Pokémon completely (database + file)")
            .WithDescription("Removes the Pokémon, all its related data and the original file from disk. Irreversible operation.")
            .WithTags("Pokemon", "Admin")
            .Produces<object>(200)
            .Produces(404);

            // ...resto de endpoints (get, patch, etc.)...

            // Main Pokemon query endpoint with advanced filtering, sorting and pagination
            app.MapGet("/pokemon", async (AppDbContext db, [AsParameters] AdvancedPokemonQuery q) =>
            {
                var baseQuery = db.Pokemon.AsNoTracking().AsQueryable();

                // Apply advanced filtering and sorting
                var query = PokemonQueryService.BuildQuery(baseQuery, q);

                var total = await query.CountAsync();
                var items = await query
                    .Skip(q.Skip)
                    .Take(q.Take)
                    .Join(db.Files, p => p.FileId, f => f.Id, (p, f) => new { Pokemon = p, File = f })
                    .Select(pf => new
                    {
                        Id = pf.Pokemon.Id,
                        SpeciesId = pf.Pokemon.SpeciesId,
                        Form = PokemonFormService.GetDisplayForm(pf.Pokemon, pf.File.Format),
                        Nickname = pf.Pokemon.Nickname,
                        Level = pf.Pokemon.Level,
                        IsShiny = pf.Pokemon.IsShiny,
                        BallId = pf.Pokemon.BallId,
                        TeraType = pf.Pokemon.TeraType,
                        HeldItemId = pf.Pokemon.HeldItemId,
                        Gender = pf.Pokemon.Gender,
                        SpriteKey = pf.Pokemon.SpriteKey,
                        OriginGeneration = PokemonGameInfoService.GetSpeciesOriginGeneration(pf.Pokemon.SpeciesId),
                        CapturedGeneration = PokemonGameInfoService.GetCapturedGeneration(pf.Pokemon.OriginGame, pf.File.Format),
                        CanGigantamax = pf.Pokemon.CanGigantamax,
                        HasMegaStone = PokemonFormService.CheckHasMegaStone(pf.Pokemon)
                    })
                    .ToListAsync();

                // Get all Pokemon IDs to fetch their tags
                var pokemonIds = items.Select(i => i.Id).ToList();
                var pokemonTags = await db.PokemonTags
                    .Where(pt => pokemonIds.Contains(pt.PokemonId))
                    .Include(pt => pt.Tag)
                    .GroupBy(pt => pt.PokemonId)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.Select(pt => new TagDto
                        {
                            Id = pt.Tag.Id,
                            Name = pt.Tag.Name,
                            ImagePath = pt.Tag.ImagePath
                        })
                        .OrderBy(t => t.Name)
                        .ToList()
                    );

                // Create the final DTOs with tags
                var resultItems = items.Select(item => new PokemonListItemDto
                {
                    Id = item.Id,
                    SpeciesId = item.SpeciesId,
                    Form = item.Form,
                    Nickname = item.Nickname,
                    Level = item.Level,
                    IsShiny = item.IsShiny,
                    BallId = item.BallId,
                    TeraType = item.TeraType,
                    HeldItemId = item.HeldItemId,
                    Gender = item.Gender,
                    SpriteKey = item.SpriteKey,
                    OriginGeneration = item.OriginGeneration,
                    CapturedGeneration = item.CapturedGeneration,
                    CanGigantamax = item.CanGigantamax,
                    HasMegaStone = item.HasMegaStone,
                    Tags = pokemonTags.GetValueOrDefault(item.Id, new List<TagDto>())
                }).ToList();

                // Get query statistics for monitoring
                var stats = PokemonQueryService.GetQueryStats(q);

                return Results.Ok(new
                {
                    Items = resultItems,
                    Total = total,
                    Stats = stats
                });
            })
            .WithName("GetPokemonList")
            .WithSummary("Get Pokemon with advanced filtering, sorting and pagination")
            .WithDescription("Main endpoint with comprehensive filtering by types, generations, stats, and flexible sorting options.")
            .WithTags("Pokemon")
            .Produces<object>(200);

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
            .Produces<object>(200);

            app.MapGet("/pokemon/{id:int}", async (int id, AppDbContext db) =>
            {
                var p = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Results.NotFound();
                var stats = await db.Stats.AsNoTracking().FirstOrDefaultAsync(x => x.PokemonId == p.Id);
                var moves = await db.Moves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();
                var relearnMoves = await db.RelearnMoves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();
                return Results.Ok(new PokemonDetailDto(p, stats, moves, relearnMoves));
            })
            .WithName("GetPokemonById")
            .WithSummary("Get complete details of a Pokémon")
            .WithDescription("Returns all data of a specific Pokémon including stats, moves and relearn moves.")
            .WithTags("Pokemon")
            .Produces<PokemonDetailDto>(200)
            .Produces(404);

            app.MapGet("/pokemon/{id:int}/showdown", async (int id, AppDbContext db) =>
            {
                var p = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Results.NotFound();
                var stats = await db.Stats.AsNoTracking().FirstOrDefaultAsync(x => x.PokemonId == p.Id);
                var moves = await db.Moves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();
                var text = BeastVault.Api.Domain.ValueObjects.ShowdownExport.From(p, stats, moves);
                return Results.Text(text);
            })
            .WithName("ExportPokemonShowdown")
            .WithSummary("Export a Pokémon in Pokémon Showdown format")
            .WithDescription("Generates a Pokémon Showdown set with all the Pokémon data (moves, stats, item, etc.).")
            .WithTags("Pokemon")
            .Produces<string>(200, "text/plain")
            .Produces(404);

            app.MapPatch("/pokemon/{id:int}", async (int id, UpdatePokemonDto dto, AppDbContext db) =>
            {
                var p = await db.Pokemon.FirstOrDefaultAsync(x => x.Id == id);
                if (p == null) return Results.NotFound();
                if (dto.Favorite.HasValue) p.Favorite = dto.Favorite.Value;
                if (dto.Notes is not null) p.Notes = dto.Notes;
                await db.SaveChangesAsync();
                return Results.NoContent();
            })
            .WithName("UpdatePokemon")
            .WithSummary("Update Pokémon properties")
            .WithDescription("Allows updating editable fields like favorite and notes. Only provided fields in the DTO are updated.")
            .WithTags("Pokemon")
            .Accepts<UpdatePokemonDto>("application/json")
            .Produces(204)
            .Produces(404);

            // Compare two Pokemon to see differences (useful for debugging trades)
            app.MapGet("/pokemon/compare/{id1:int}/{id2:int}", async (int id1, int id2, AppDbContext db) =>
            {
                var p1 = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id1);
                var p2 = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id2);

                if (p1 == null || p2 == null)
                    return Results.NotFound("One or both Pokemon not found");

                var comparison = PokemonComparisonService.Compare(p1, p2);

                return Results.Ok(new
                {
                    Pokemon1 = new { Id = p1.Id, Species = PkHexStringService.GetSpeciesName(p1.SpeciesId), Nickname = p1.Nickname },
                    Pokemon2 = new { Id = p2.Id, Species = PkHexStringService.GetSpeciesName(p2.SpeciesId), Nickname = p2.Nickname },
                    AreIdentical = comparison.AreIdentical,
                    Differences = comparison.Differences,
                    Summary = comparison.AreIdentical ? "Pokemon are identical" : $"Found {comparison.Differences.Count} differences"
                });
            })
            .WithName("ComparePokemon")
            .WithSummary("Compare two Pokémon and show differences")
            .WithDescription("Analyzes and compares all fields of two different Pokémon. Useful for detecting changes after trades or edits.")
            .WithTags("Pokemon", "Comparison")
            .Produces<object>(200)
            .Produces(404);

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
            .WithTags("Debug");

            return app;
        }
    }
}
