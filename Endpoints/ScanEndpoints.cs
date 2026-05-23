using Microsoft.AspNetCore.Mvc;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Helpers;

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
    }
}
