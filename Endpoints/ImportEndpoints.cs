using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Endpoints
{
    public static class ImportEndpoints
    {
        /// <summary>
        /// Import one or multiple .pk* Pokémon files. Uses multipart/form-data.
        /// </summary>
        public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/import",
                [Consumes("multipart/form-data")]
            async ([FromForm] IFormFileCollection files, AppDbContext db, FileStorageService storage, PkhexCoreParser parser) =>
            {
                if (files == null || files.Count == 0)
                    return Results.BadRequest("No files provided.");

                var imported = new List<object>();

                foreach (var f in files)
                {
                    if (f.Length == 0) continue;

                    await using var ms = new MemoryStream();
                    await f.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    // Log to verify we save the original bytes
                    Console.WriteLine($"Original file: {f.FileName}, Size: {bytes.Length} bytes, SHA256: {FileStorageService.ComputeSha256(bytes)}");

                    var parse = await parser.ParseAsync(bytes, f.FileName, storage);
                    if (parse is null)
                    {
                        imported.Add(new { FileName = f.FileName, Status = "error", Message = "Could not parse file" });
                        continue;
                    }

                    // Dedupe by hash
                    var existingFile = await db.Files.FirstOrDefaultAsync(x => x.Sha256 == parse.File.Sha256);
                    if (existingFile != null)
                    {
                        imported.Add(new { FileName = f.FileName, Status = "duplicate", PokemonId = existingFile.Id });
                        continue;
                    }


                    // File already parsed and saved by ParseAsync
                    if (string.IsNullOrEmpty(parse.File.StoredPath))
                    {
                        imported.Add(new { FileName = f.FileName, Status = "error", Message = "Failed to save file to storage" });
                        continue;
                    }

                    // Save backup in database
                    parse.File.RawBlob = bytes;

                    // Persistence
                    db.Files.Add(parse.File);
                    await db.SaveChangesAsync();

                    parse.Pokemon.FileId = parse.File.Id;
                    db.Pokemon.Add(parse.Pokemon);
                    await db.SaveChangesAsync();

                    if (parse.Stats != null)
                    {
                        parse.Stats.PokemonId = parse.Pokemon.Id;
                        db.Stats.Add(parse.Stats);
                    }
                    if (parse.Moves.Any())
                    {
                        foreach (var m in parse.Moves)
                        {
                            m.PokemonId = parse.Pokemon.Id;
                            db.Moves.Add(m);
                        }
                    }
                    if (parse.RelearnMoves.Any())
                    {
                        foreach (var rm in parse.RelearnMoves)
                        {
                            rm.PokemonId = parse.Pokemon.Id;
                            db.RelearnMoves.Add(rm);
                        }
                    }
                    await db.SaveChangesAsync();

                    imported.Add(new
                    {
                        FileName = f.FileName,
                        Status = "imported",
                        PokemonId = parse.Pokemon.Id,
                        Pokemon = parse.Pokemon,
                        Stats = parse.Stats,
                        Moves = parse.Moves,
                        RelearnMoves = parse.RelearnMoves
                    });
                }

                return Results.Ok(imported);
            })
            .WithName("ImportPokemonFiles")
            .WithSummary("Import PKM files (.pk9, .pb9, .pa9, .pa8, .pk8, etc.)")
            .WithDescription("Import one or multiple Pokémon files in PKM format. Supports all formats (.pk1 to .pk9, .pb7/.pb8/.pb9, .pa8/.pa9). Files are stored both on disk and in database to preserve the original.")
            .WithTags("Import")
            .Accepts<IFormFileCollection>("multipart/form-data")
            .Produces<List<object>>(200)
            .Produces(400)
            .DisableAntiforgery();

            return app;
        }
    }
}
