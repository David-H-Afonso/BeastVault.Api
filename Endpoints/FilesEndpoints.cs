using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Infrastructure.Helpers;

namespace BeastVault.Api.Endpoints
{
    public static class FilesEndpoints
    {
        public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/files/{id:int}", async (int id, HttpContext httpContext, AppDbContext db, FileStorageService storage) =>
            {
                var userId = httpContext.GetUserIdOrDefault();
                var f = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
                if (f == null) return Results.NotFound();

                try
                {
                    var bytes = storage.Read(f.StoredPath);
                    var contentType = "application/octet-stream";
                    var downloadName = f.FileName;
                    return Results.File(bytes, contentType, downloadName);
                }
                catch (FileNotFoundException)
                {
                    return Results.Problem($"File not found on disk: {f.StoredPath}");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error reading file: {ex.Message}");
                }
            })
            .WithName("GetFileById")
            .WithSummary("Download a stored file by its internal ID")
            .WithDescription("Returns the original uploaded file using the file ID. Useful for auditing or retrieving individual files.")
            .WithTags("Files")
            .RequireAuthorization()
            .Produces<byte[]>(200, "application/octet-stream")
            .Produces(404);

            app.MapGet("/export/{pokemonId:int}", async (int pokemonId, HttpContext httpContext, AppDbContext db) =>
            {
                var userId = httpContext.GetUserIdOrDefault();
                var poke = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
                if (poke == null) return Results.NotFound();
                var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == poke.FileId);
                if (file == null || file.RawBlob == null || file.RawBlob.Length == 0)
                    return Results.Problem("Backup not found in database.");

                // Validate PKM (optional)
                try
                {
                    var pk = PKHeX.Core.EntityFormat.GetFromBytes(file.RawBlob);
                    if (pk == null) return Results.Problem("The file is not a valid PKM.");
                }
                catch { return Results.Problem("The file is not a valid PKM."); }

                // Use the original file name for download
                var downloadName = file.FileName;
                return Results.File(file.RawBlob, "application/octet-stream", downloadName);
            })
            .WithName("ExportPokemonOriginal")
            .WithSummary("Download the original PKM file of a Pokémon")
            .WithDescription("Returns the original PKM file (.pk9, .pk8, etc.) stored in the database for the specified Pokémon. The file is identical to the one initially uploaded.")
            .WithTags("Files")
            .RequireAuthorization()
            .Produces<byte[]>(200, "application/octet-stream")
            .Produces(404)
            .Produces(500);

            app.MapGet("/export/database/{pokemonId:int}", async (int pokemonId, HttpContext httpContext, AppDbContext db, FileStorageService storage) =>
            {
                var userId = httpContext.GetUserIdOrDefault();
                var poke = await db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
                if (poke == null) return Results.NotFound();
                var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == poke.FileId);
                if (file == null) return Results.NotFound();

                try
                {
                    var bytes = storage.Read(file.StoredPath);
                    if (bytes == null || bytes.Length == 0) return Results.Problem("File is empty.");

                    // Validate PKM (optional)
                    try
                    {
                        var pk = PKHeX.Core.EntityFormat.GetFromBytes(bytes);
                        if (pk == null) return Results.Problem("The file is not a valid PKM.");
                    }
                    catch { return Results.Problem("The file is not a valid PKM."); }

                    // Use the original file name for download
                    var downloadName = file.FileName;
                    return Results.File(bytes, "application/octet-stream", downloadName);
                }
                catch (FileNotFoundException)
                {
                    return Results.Problem($"File not found on disk: {file.StoredPath}");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error reading file: {ex.Message}");
                }
            })
            .WithName("ExportPokemonFromDisk")
            .WithSummary("Download the PKM file from disk (audit)")
            .WithDescription("Returns the PKM file stored on disk for the specified Pokémon. Useful for comparing the database backup vs. the file on disk.")
            .WithTags("Files")
            .RequireAuthorization()
            .Produces<byte[]>(200, "application/octet-stream")
            .Produces(404)
            .Produces(500);
            // Download backup file by Pokemon ID
            app.MapGet("/export/backup/{pokemonId:int}", async (int pokemonId, HttpContext httpContext, AppDbContext context, FileStorageService storage) =>
            {
                var userId = httpContext.GetUserIdOrDefault();
                var pokemon = await context.Pokemon
                    .Include(p => p.File)
                    .FirstOrDefaultAsync(p => p.Id == pokemonId && p.UserId == userId);

                if (pokemon?.File == null)
                    return Results.NotFound("Pokemon or file not found");

                // Get the original extension
                var originalExt = Path.GetExtension(pokemon.File.OriginalFileName ?? pokemon.File.FileName);

                // Try to get backup based on file import date
                var importDate = pokemon.File.ImportedAt;
                var fileName = pokemon.File.OriginalFileName ?? pokemon.File.FileName;

                try
                {
                    var backupPath = storage.GetBackupPath(userId, fileName, originalExt, importDate);

                    if (!File.Exists(backupPath))
                        return Results.NotFound("Backup file not found");

                    var fileBytes = await File.ReadAllBytesAsync(backupPath);
                    var downloadName = $"backup_{fileName}";

                    return Results.File(fileBytes, "application/octet-stream", downloadName);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error accessing backup: {ex.Message}");
                }
            })
            .WithName("DownloadBackupFile")
            .RequireAuthorization()
            .WithTags("Export");

            return app;
        }
    }
}
