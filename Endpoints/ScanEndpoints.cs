using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Helpers;
using PKHeX.Core;

namespace BeastVault.Api.Endpoints
{
    public static class ScanEndpoints
    {
        public static void MapScanEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/scan")
                .WithTags("File Scanning")
                .RequireAuthorization();

            group.MapPost("/directory", ScanDirectory)
                .WithName("ScanDirectory")
                .WithSummary("Scan Documents/BeastVault for new Pokemon files")
                .WithDescription(@"
Scans the user's Documents/BeastVault directory for new Pokemon files and automatically imports them.
Files already in the database (based on SHA256 hash) will be skipped.

Supported file formats:
- .pk1, .pk2, .pk3, .pk4, .pk5, .pk6, .pk7, .pk8, .pk9 (Standard PKM files)
- .pb7, .pb8, .pb9 (Pokemon Box files)
- .pa8 (Legends Arceus)
- .pa9 (Legends Z-A)
- .ek1, .ek2, .ek3, .ek4, .ek5, .ek6, .ek7, .ek8, .ek9 (Encrypted)
- .ekx (Encrypted batch)
");

            group.MapGet("/status", GetScanStatus)
                .WithName("GetScanStatus")
                .WithSummary("Get information about the scan directory")
                .WithDescription("Returns information about the Documents/BeastVault directory and file counts.");

            group.MapPost("/refresh", RefreshPokemonData)
                .WithName("RefreshPokemonData")
                .WithSummary("Re-parse all stored Pokemon files and update metadata")
                .WithDescription("Re-reads the raw PKM blobs from the database and updates friendship, met level, met location, and other fields that may have been incorrectly parsed previously.");
        }

        private static async Task<IResult> ScanDirectory(
            [FromServices] FileWatcherService fileWatcher,
            [FromServices] FileStorageService storage,
            HttpContext ctx)
        {
            try
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                // Ensure user's directory exists
                storage.EnsureUserVault(userId.Value);

                // Scan only the current user's directory
                var result = await fileWatcher.ScanUserDirectoryAsync(userId.Value);

                return Results.Ok(new
                {
                    Success = true,
                    Summary = new
                    {
                        TotalProcessed = result.TotalProcessed,
                        NewlyImported = result.NewlyImported.Count,
                        AlreadyImported = result.AlreadyImported.Count,
                        Deleted = result.Deleted.Count,
                        Errors = result.Errors.Count
                    },
                    Details = new
                    {
                        NewlyImported = result.NewlyImported,
                        AlreadyImported = result.AlreadyImported,
                        Deleted = result.Deleted,
                        Errors = result.Errors
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private static IResult GetScanStatus()
        {
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var watchPath = Path.Combine(documentsPath, "BeastVault");

                if (!Directory.Exists(watchPath))
                {
                    return Results.Ok(new
                    {
                        DirectoryExists = false,
                        WatchPath = watchPath,
                        Message = "Scan directory does not exist. It will be created on first scan."
                    });
                }

                // Count Pokemon files
                var pokemonFiles = Directory.GetFiles(watchPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => IsPokemonFile(file))
                    .ToList();

                var filesByExtension = pokemonFiles
                    .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.Count());

                return Results.Ok(new
                {
                    DirectoryExists = true,
                    WatchPath = watchPath,
                    TotalPokemonFiles = pokemonFiles.Count,
                    FilesByExtension = filesByExtension,
                    LastModified = Directory.GetLastWriteTime(watchPath)
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new
                {
                    Error = ex.Message
                });
            }
        }

        private static bool IsPokemonFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pk1" or ".pk2" or ".pk3" or ".pk4" or ".pk5" or ".pk6" or ".pk7" or ".pk8" or ".pk9" => true,
                ".pb7" or ".pb8" or ".pb9" => true, // Pokemon Box formats
                ".pa8" => true, // Legends Arceus
                ".pa9" => true, // Legends Z-A
                ".ek1" or ".ek2" or ".ek3" or ".ek4" or ".ek5" or ".ek6" or ".ek7" or ".ek8" or ".ek9" => true,
                ".ekx" => true,
                _ => false
            };
        }

        private static async Task<IResult> RefreshPokemonData(
            [FromServices] AppDbContext db,
            [FromServices] PkhexCoreParser parser,
            HttpContext ctx)
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            // Get all pokemon for this user joined with their file blobs
            var pokemonWithFiles = await db.Pokemon
                .Where(p => p.UserId == userId)
                .Join(db.Files.Where(f => f.RawBlob != null),
                    p => p.FileId, f => f.Id,
                    (p, f) => new { Pokemon = p, File = f })
                .ToListAsync();

            int updated = 0, errors = 0;

            foreach (var pf in pokemonWithFiles)
            {
                var p = pf.Pokemon;
                var rawBlob = pf.File.RawBlob;
                if (rawBlob == null || rawBlob.Length == 0) continue;

                try
                {
                    var parsed = await parser.ParseAsync(rawBlob, pf.File.OriginalFileName ?? pf.File.FileName);
                    if (parsed == null) continue;
                    var pk = EntityFormat.GetFromBytes(rawBlob.ToArray());
                    if (pk == null) continue;

                    p.Language = parsed.Pokemon.Language;
                    p.OTLanguage = parsed.Pokemon.OTLanguage;
                    p.MetDate = parsed.Pokemon.MetDate;
                    p.Tid = parsed.Pokemon.Tid;
                    p.Sid = parsed.Pokemon.Sid;
                    p.OtName = parsed.Pokemon.OtName;

                    if (parsed.Stats is not null)
                    {
                        var existingStats = await db.Stats.FirstOrDefaultAsync(s => s.PokemonId == p.Id);
                        if (existingStats is null)
                        {
                            parsed.Stats.PokemonId = p.Id;
                            db.Stats.Add(parsed.Stats);
                        }
                        else
                        {
                            existingStats.StatHp = parsed.Stats.StatHp;
                            existingStats.StatAtk = parsed.Stats.StatAtk;
                            existingStats.StatDef = parsed.Stats.StatDef;
                            existingStats.StatSpa = parsed.Stats.StatSpa;
                            existingStats.StatSpd = parsed.Stats.StatSpd;
                            existingStats.StatSpe = parsed.Stats.StatSpe;
                            existingStats.StatHpCurrent = parsed.Stats.StatHpCurrent;
                        }
                    }

                    // Update fields using Convert to handle byte/ushort/int differences
                    var metLevelProp = pk.GetType().GetProperty("MetLevel") ?? pk.GetType().GetProperty("Met_Level");
                    if (metLevelProp != null)
                    {
                        var val = metLevelProp.GetValue(pk);
                        if (val != null) p.MetLevel = Convert.ToInt32(val);
                    }

                    var friendProp = pk.GetType().GetProperty("CurrentFriendship");
                    if (friendProp != null)
                    {
                        var val = friendProp.GetValue(pk);
                        if (val != null) p.CurrentFriendship = Convert.ToInt32(val);
                    }

                    var htFriendProp = pk.GetType().GetProperty("HandlingTrainerFriendship") ?? pk.GetType().GetProperty("HT_Friendship");
                    if (htFriendProp != null)
                    {
                        var val = htFriendProp.GetValue(pk);
                        if (val != null) p.HandlingTrainerFriendship = Convert.ToInt32(val);
                    }

                    var handlerProp = pk.GetType().GetProperty("CurrentHandler");
                    if (handlerProp != null)
                    {
                        var val = handlerProp.GetValue(pk);
                        if (val != null) p.CurrentHandler = Convert.ToInt32(val);
                    }

                    var metLocProp = pk.GetType().GetProperty("MetLocation") ?? pk.GetType().GetProperty("Met_Location");
                    if (metLocProp != null)
                    {
                        var val = metLocProp.GetValue(pk);
                        if (val != null)
                        {
                            var metLoc = Convert.ToInt32(val);
                            if (metLoc > 0)
                            {
                                try
                                {
                                    var locationNames = GameInfo.GetLocationList((GameVersion)pk.Version, pk.Context, false);
                                    var match = locationNames.FirstOrDefault(l => l.Value == metLoc);
                                    p.MetLocation = match != null && !string.IsNullOrWhiteSpace(match.Text) ? match.Text : $"Location {metLoc}";
                                }
                                catch { p.MetLocation = $"Location {metLoc}"; }
                            }
                        }
                    }

                    updated++;
                }
                catch
                {
                    errors++;
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Refreshed {updated} Pokémon, {errors} errors",
                updated,
                errors,
                total = pokemonWithFiles.Count
            });
        }
    }
}
