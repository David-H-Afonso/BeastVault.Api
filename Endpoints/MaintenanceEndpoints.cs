using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Endpoints
{
    public static class MaintenanceEndpoints
    {
        public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/maintenance/sync", async (AppDbContext db, FileStorageService storage) =>
            {
                var result = new SyncResult();

                try
                {
                    // Get all files in database
                    var allDbFiles = await db.Files.ToListAsync();
                    result.TotalFilesInDatabase = allDbFiles.Count;

                    foreach (var dbFile in allDbFiles)
                    {
                        // Check if the stored file still exists
                        var fileExists = !string.IsNullOrEmpty(dbFile.StoredPath) && File.Exists(dbFile.StoredPath);

                        if (!fileExists)
                        {
                            // File no longer exists, remove from database
                            var pokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.FileId == dbFile.Id);
                            if (pokemon != null)
                            {
                                // Remove related data
                                var stats = await db.Stats.FirstOrDefaultAsync(s => s.PokemonId == pokemon.Id);
                                if (stats != null) db.Stats.Remove(stats);

                                var moves = await db.Moves.Where(m => m.PokemonId == pokemon.Id).ToListAsync();
                                db.Moves.RemoveRange(moves);

                                var relearnMoves = await db.RelearnMoves.Where(r => r.PokemonId == pokemon.Id).ToListAsync();
                                db.RelearnMoves.RemoveRange(relearnMoves);

                                db.Pokemon.Remove(pokemon);
                                result.RemovedPokemon.Add(pokemon.Nickname ?? $"Species #{pokemon.SpeciesId}");
                            }

                            db.Files.Remove(dbFile);
                            result.RemovedFiles.Add(dbFile.FileName);
                            Console.WriteLine($"Removed orphaned file from database: {dbFile.FileName} (path: {dbFile.StoredPath})");
                        }
                        else
                        {
                            result.ValidFiles.Add(dbFile.FileName);
                        }
                    }

                    if (result.RemovedFiles.Any() || result.RemovedPokemon.Any())
                    {
                        await db.SaveChangesAsync();
                        Console.WriteLine($"Database sync completed: Removed {result.RemovedFiles.Count} files and {result.RemovedPokemon.Count} Pokemon");
                    }

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during database sync: {ex.Message}");
                    return Results.Problem($"Sync failed: {ex.Message}");
                }
            })
            .WithName("SyncDatabase")
            .WithSummary("Synchronize database with file system")
            .WithDescription("Removes orphaned entries from database when files no longer exist on disk")
            .WithTags("Maintenance")
            .Produces<SyncResult>(200)
            .Produces(500)
            .RequireAuthorization("AdminPolicy");

            app.MapGet("/maintenance/status", async (AppDbContext db, FileStorageService storage) =>
            {
                var result = new MaintenanceStatusResult();

                try
                {
                    // Get database counts
                    result.TotalPokemonInDatabase = await db.Pokemon.CountAsync();
                    result.TotalFilesInDatabase = await db.Files.CountAsync();

                    // Check file system status
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var backupPath = Path.Combine(documentsPath, "BeastVault", "backup");

                    if (Directory.Exists(backupPath))
                    {
                        var pokemonFiles = Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories)
                            .Where(file => IsPokemonFile(file))
                            .ToList();

                        result.TotalFilesInBackupDirectory = pokemonFiles.Count;
                        result.BackupDirectoryPath = backupPath;
                    }
                    else
                    {
                        result.TotalFilesInBackupDirectory = 0;
                        result.BackupDirectoryPath = $"{backupPath} (does not exist)";
                    }

                    // Check for orphaned entries
                    var allDbFiles = await db.Files.ToListAsync();
                    foreach (var dbFile in allDbFiles)
                    {
                        var fileExists = !string.IsNullOrEmpty(dbFile.StoredPath) && File.Exists(dbFile.StoredPath);
                        if (!fileExists)
                        {
                            result.OrphanedFiles.Add(new OrphanedFileInfo
                            {
                                FileName = dbFile.FileName,
                                StoredPath = dbFile.StoredPath ?? "null",
                                Id = dbFile.Id
                            });
                        }
                    }

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting maintenance status: {ex.Message}");
                    return Results.Problem($"Status check failed: {ex.Message}");
                }
            })
            .WithName("GetMaintenanceStatus")
            .WithSummary("Get database and file system status")
            .WithDescription("Shows counts and identifies orphaned entries")
            .WithTags("Maintenance")
            .Produces<MaintenanceStatusResult>(200)
            .Produces(500)
            .RequireAuthorization("AdminPolicy");

            // GET endpoint to check for duplicates of a specific Pokemon by ID
            app.MapGet("/maintenance/pokemon/{id}/duplicates", async (int id, AppDbContext db) =>
            {
                try
                {
                    var pokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Id == id);
                    if (pokemon == null)
                    {
                        return Results.NotFound($"Pokemon with ID {id} not found");
                    }

                    var file = await db.Files.FirstOrDefaultAsync(f => f.Id == pokemon.FileId);
                    if (file == null)
                    {
                        return Results.Problem("Associated file not found");
                    }

                    // Find all files with the same hash (same Pokemon)
                    var duplicateFiles = await db.Files
                        .Where(f => f.Sha256 == file.Sha256)
                        .ToListAsync();

                    // Get associated Pokemon for these files
                    var fileIds = duplicateFiles.Select(f => f.Id).ToList();
                    var associatedPokemon = await db.Pokemon
                        .Where(p => fileIds.Contains(p.FileId))
                        .ToListAsync();

                    // Find all physical files in user area and backup that match
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var beastVaultPath = Path.Combine(documentsPath, "BeastVault");
                    var backupPath = Path.Combine(beastVaultPath, "backup");

                    var physicalFiles = new List<string>();
                    var backupFiles = new List<string>();

                    if (Directory.Exists(beastVaultPath))
                    {
                        var allFiles = Directory.GetFiles(beastVaultPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => IsPokemonFile(f))
                            .ToList();

                        foreach (var physicalFile in allFiles)
                        {
                            try
                            {
                                var fileBytes = await File.ReadAllBytesAsync(physicalFile);
                                var hash = FileStorageService.ComputeSha256(fileBytes);
                                if (hash == file.Sha256)
                                {
                                    var isInBackup = physicalFile.StartsWith(backupPath, StringComparison.OrdinalIgnoreCase);
                                    if (isInBackup)
                                    {
                                        backupFiles.Add(physicalFile);
                                    }
                                    else
                                    {
                                        physicalFiles.Add(physicalFile);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error reading file {physicalFile}: {ex.Message}");
                            }
                        }
                    }

                    var allPhysicalFiles = new List<string>();
                    allPhysicalFiles.AddRange(physicalFiles);
                    allPhysicalFiles.AddRange(backupFiles);

                    var result = new PokemonDuplicatesInfo
                    {
                        PokemonId = pokemon.Id,
                        PokemonName = pokemon.Nickname ?? $"Species #{pokemon.SpeciesId}",
                        FileHash = file.Sha256,
                        DatabaseEntries = duplicateFiles.Count,
                        PhysicalFiles = physicalFiles.Count, // Only count user files, not backup
                        PhysicalFilePaths = allPhysicalFiles, // Show all paths for reference
                        DatabaseFileIds = duplicateFiles.Select(f => f.Id).ToList(),
                        IsInBackup = backupFiles.Any()
                    };

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking duplicates: {ex.Message}");
                    return Results.Problem($"Duplicate check failed: {ex.Message}");
                }
            })
            .WithName("GetPokemonDuplicates")
            .WithSummary("Check for duplicates of a specific Pokemon")
            .WithDescription("Returns information about database entries and physical files for the same Pokemon")
            .WithTags("Maintenance")
            .Produces<PokemonDuplicatesInfo>(200)
            .Produces(404)
            .Produces(500)
            .RequireAuthorization("AdminPolicy");

            // DELETE endpoint for total elimination
            app.MapDelete("/maintenance/pokemon/{id}/total", async (
                int id,
                int expectedFileCount,
                bool includeBackup,
                AppDbContext db,
                FileStorageService storage) =>
            {
                try
                {
                    var pokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Id == id);
                    if (pokemon == null)
                    {
                        return Results.NotFound($"Pokemon with ID {id} not found");
                    }

                    var file = await db.Files.FirstOrDefaultAsync(f => f.Id == pokemon.FileId);
                    if (file == null)
                    {
                        return Results.Problem("Associated file not found");
                    }

                    // Find all files with the same hash
                    var duplicateFiles = await db.Files
                        .Where(f => f.Sha256 == file.Sha256)
                        .ToListAsync();

                    // Count actual physical files for verification
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    var beastVaultPath = Path.Combine(documentsPath, "BeastVault");
                    var backupPath = Path.Combine(beastVaultPath, "backup");

                    var physicalFiles = new List<string>();
                    var backupFiles = new List<string>();

                    if (Directory.Exists(beastVaultPath))
                    {
                        var allFiles = Directory.GetFiles(beastVaultPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => IsPokemonFile(f))
                            .ToList();

                        foreach (var physicalFile in allFiles)
                        {
                            try
                            {
                                var fileBytes = await File.ReadAllBytesAsync(physicalFile);
                                var hash = FileStorageService.ComputeSha256(fileBytes);
                                if (hash == file.Sha256)
                                {
                                    var isInBackup = physicalFile.StartsWith(backupPath, StringComparison.OrdinalIgnoreCase);
                                    if (isInBackup)
                                    {
                                        backupFiles.Add(physicalFile);
                                    }
                                    else
                                    {
                                        physicalFiles.Add(physicalFile);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error reading file {physicalFile} for deletion: {ex.Message}");
                            }
                        }
                    }

                    var totalPhysicalFiles = physicalFiles.Count + (includeBackup ? backupFiles.Count : 0);

                    // Safety check: verify expected file count against actual physical files
                    if (totalPhysicalFiles != expectedFileCount)
                    {
                        return Results.BadRequest($"Expected {expectedFileCount} physical files, but found {totalPhysicalFiles} ({physicalFiles.Count} user files + {backupFiles.Count} backup files). Please refresh and try again.");
                    }

                    var result = new TotalDeletionResult
                    {
                        DeletedFromDatabase = duplicateFiles.Count,
                        IncludedBackup = includeBackup
                    };

                    // Remove from database
                    foreach (var dbFile in duplicateFiles)
                    {
                        var pokemonEntry = await db.Pokemon.FirstOrDefaultAsync(p => p.FileId == dbFile.Id);
                        if (pokemonEntry != null)
                        {
                            // Remove related data
                            var stats = await db.Stats.FirstOrDefaultAsync(s => s.PokemonId == pokemonEntry.Id);
                            if (stats != null) db.Stats.Remove(stats);

                            var moves = await db.Moves.Where(m => m.PokemonId == pokemonEntry.Id).ToListAsync();
                            db.Moves.RemoveRange(moves);

                            var relearnMoves = await db.RelearnMoves.Where(r => r.PokemonId == pokemonEntry.Id).ToListAsync();
                            db.RelearnMoves.RemoveRange(relearnMoves);

                            db.Pokemon.Remove(pokemonEntry);
                            result.DeletedPokemonNames.Add(pokemonEntry.Nickname ?? $"Species #{pokemonEntry.SpeciesId}");
                        }

                        db.Files.Remove(dbFile);
                        result.DeletedDatabaseFiles.Add(dbFile.FileName);
                    }

                    // Remove physical files
                    if (Directory.Exists(beastVaultPath))
                    {
                        var allFiles = Directory.GetFiles(beastVaultPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => IsPokemonFile(f))
                            .ToList();

                        foreach (var physicalFile in allFiles)
                        {
                            try
                            {
                                var fileBytes = await File.ReadAllBytesAsync(physicalFile);
                                var hash = FileStorageService.ComputeSha256(fileBytes);
                                if (hash == file.Sha256)
                                {
                                    // Check if it's in backup and if we should include it
                                    var isInBackup = physicalFile.StartsWith(backupPath, StringComparison.OrdinalIgnoreCase);

                                    if (!isInBackup || includeBackup)
                                    {
                                        if (isInBackup && includeBackup)
                                        {
                                            // Use DeleteBackup method for backup files
                                            var fileName = Path.GetFileName(physicalFile);
                                            var ext = Path.GetExtension(physicalFile);
                                            try
                                            {
                                                storage.DeleteBackup(fileName, ext);
                                            }
                                            catch (Exception)
                                            {
                                                // Fallback to direct deletion if DeleteBackup fails
                                                File.Delete(physicalFile);
                                                Console.WriteLine($"Used fallback deletion for backup file: {physicalFile}");
                                            }
                                        }
                                        else
                                        {
                                            // Regular file deletion for non-backup files
                                            File.Delete(physicalFile);
                                        }
                                        result.DeletedPhysicalFiles.Add(physicalFile);
                                    }
                                    else
                                    {
                                        result.PreservedBackupFiles.Add(physicalFile);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Errors.Add($"Error deleting {physicalFile}: {ex.Message}");
                            }
                        }
                    }

                    await db.SaveChangesAsync();

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during total deletion: {ex.Message}");
                    return Results.Problem($"Total deletion failed: {ex.Message}");
                }
            })
            .WithName("DeletePokemonTotal")
            .WithSummary("Completely delete a Pokemon and all its duplicates")
            .WithDescription("Removes from database and optionally from backup. Requires expected file count for safety.")
            .WithTags("Maintenance")
            .Produces<TotalDeletionResult>(200)
            .Produces(404)
            .Produces(500)
            .RequireAuthorization("AdminPolicy");

            return app;
        }

        private static bool IsPokemonFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pk1" or ".pk2" or ".pk3" or ".pk4" or ".pk5" or ".pk6" or ".pk7" or ".pk8" or ".pk9" => true,
                ".pgt" or ".pgf" or ".wc3" or ".wc4" or ".wc5" or ".wc6" or ".wc7" or ".wc8" or ".wc9" => true,
                _ => false
            };
        }
    }
}
