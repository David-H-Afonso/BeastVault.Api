using System.Text.Json;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Endpoints;

public static class SpriteEndpoints
{
    public static void MapSpriteEndpoints(this WebApplication app)
    {
        // ── Cached sprites served from DB (downloaded from PokéAPI) ─────────────
        app.MapGet("/sprites/{**path}", async (string path, AppDbContext db, ImageCacheService imageCacheService, IHttpClientFactory httpClientFactory) =>
        {
            // ── DB-backed Pokémon sprites (lazy download on first access) ──
            var versionSpriteMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/version/(\d+)/([^/]+)/([^/]+)\.(png|gif)$");
            if (versionSpriteMatch.Success &&
                int.TryParse(versionSpriteMatch.Groups[1].Value, out int versionPokemonId))
            {
                return await ServeOrCachePokemonVersionSpriteAsync(
                    versionPokemonId,
                    versionSpriteMatch.Groups[2].Value,
                    versionSpriteMatch.Groups[3].Value,
                    versionSpriteMatch.Groups[4].Value,
                    db,
                    imageCacheService);
            }

            var spriteMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/(\d+)\.png$");
            if (spriteMatch.Success && int.TryParse(spriteMatch.Groups[1].Value, out int spriteId))
                return await ServeOrCachePokemonSpriteAsync(spriteId, SpriteKind.Default, db, httpClientFactory);

            var artworkMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/artwork/(\d+)\.png$");
            if (artworkMatch.Success && int.TryParse(artworkMatch.Groups[1].Value, out int artworkId))
                return await ServeOrCachePokemonSpriteAsync(artworkId, SpriteKind.Artwork, db, httpClientFactory);

            var shinyMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/shiny/(\d+)\.png$");
            if (shinyMatch.Success && int.TryParse(shinyMatch.Groups[1].Value, out int shinyId))
                return await ServeOrCachePokemonSpriteAsync(shinyId, SpriteKind.Shiny, db, httpClientFactory);

            var homeMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/home/(\d+)\.png$");
            if (homeMatch.Success && int.TryParse(homeMatch.Groups[1].Value, out int homeId))
                return await ServeOrCachePokemonSpriteAsync(homeId, SpriteKind.Home, db, httpClientFactory);

            var homeShinyMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/home/shiny/(\d+)\.png$");
            if (homeShinyMatch.Success && int.TryParse(homeShinyMatch.Groups[1].Value, out int homeShinyId))
                return await ServeOrCachePokemonSpriteAsync(homeShinyId, SpriteKind.HomeShiny, db, httpClientFactory);

            var showdownMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/showdown/(\d+)\.gif$");
            if (showdownMatch.Success && int.TryParse(showdownMatch.Groups[1].Value, out int showdownId))
                return await ServeOrCachePokemonSpriteAsync(showdownId, SpriteKind.Showdown, db, httpClientFactory);

            var showdownShinyMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/showdown/shiny/(\d+)\.gif$");
            if (showdownShinyMatch.Success && int.TryParse(showdownShinyMatch.Groups[1].Value, out int showdownShinyId))
                return await ServeOrCachePokemonSpriteAsync(showdownShinyId, SpriteKind.ShowdownShiny, db, httpClientFactory);

            var artworkShinyMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/artwork/shiny/(\d+)\.png$");
            if (artworkShinyMatch.Success && int.TryParse(artworkShinyMatch.Groups[1].Value, out int artworkShinyId))
                return await ServeOrCachePokemonSpriteAsync(artworkShinyId, SpriteKind.ArtworkShiny, db, httpClientFactory);

            var githubMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/github/(\d+)\.png$");
            if (githubMatch.Success && int.TryParse(githubMatch.Groups[1].Value, out int githubId))
                return await ServeOrCachePokemonSpriteAsync(githubId, SpriteKind.Github, db, httpClientFactory);

            var githubShinyMatch = System.Text.RegularExpressions.Regex.Match(path, @"^pokemon/github/shiny/(\d+)\.png$");
            if (githubShinyMatch.Success && int.TryParse(githubShinyMatch.Groups[1].Value, out int githubShinyId))
                return await ServeOrCachePokemonSpriteAsync(githubShinyId, SpriteKind.GithubShiny, db, httpClientFactory);

            // ── Auto-download ball sprites from pokesprite GitHub ──
            var ballMatch = System.Text.RegularExpressions.Regex.Match(path, @"^balls/(.+)\.png$");
            if (ballMatch.Success)
            {
                var ballSlug = ballMatch.Groups[1].Value;
                var localPath = imageCacheService.ResolveSpritePath(path);
                if (localPath != null)
                    return Results.File(localPath, "image/png");

                // Try downloading from pokesprite GitHub
                var downloaded = await imageCacheService.DownloadBallSpriteAsync(ballSlug);
                if (downloaded != null)
                    return Results.File(downloaded, "image/png");

                return Results.NotFound();
            }

            // ── Auto-download type icons from PokeAPI GitHub ──
            var typeMatch = System.Text.RegularExpressions.Regex.Match(path, @"^types/(.+)\.png$");
            if (typeMatch.Success)
            {
                var typeId = typeMatch.Groups[1].Value;
                var localPath = imageCacheService.ResolveSpritePath(path);
                if (localPath != null)
                    return Results.File(localPath, "image/png");

                var downloaded = await imageCacheService.DownloadTypeSpriteAsync(typeId);
                if (downloaded != null)
                    return Results.File(downloaded, "image/png");

                return Results.NotFound();
            }

            // ── Auto-download item sprites from pokesprite GitHub ──
            var itemMatch = System.Text.RegularExpressions.Regex.Match(path, @"^items/(.+)\.png$");
            if (itemMatch.Success)
            {
                var itemSlug = itemMatch.Groups[1].Value;
                var localPath = imageCacheService.ResolveSpritePath(path);
                if (localPath != null)
                    return Results.File(localPath, "image/png");

                // Try downloading from pokesprite GitHub
                var downloaded = await imageCacheService.DownloadItemSpriteAsync(itemSlug);
                if (downloaded != null)
                    return Results.File(downloaded, "image/png");

                return Results.NotFound();
            }

            // ── File-based fallback for legacy/custom sprites ──
            var fullPath = imageCacheService.ResolveSpritePath(path);
            if (fullPath == null) return Results.NotFound();
            var ct = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
            return Results.File(fullPath, ct, enableRangeProcessing: false);
        })
        .WithName("GetCachedSprite")
        .WithTags("Files")
        .Produces(200)
        .Produces(404)
        .AllowAnonymous();

        app.MapGet("/custom-sprites/search/{pattern}", (string pattern) =>
        {
            var assetsPath = ResolveAssetsPath();
            if (assetsPath == null)
                return Results.NotFound();

            try
            {
                var cleanPattern = Path.GetFileName(pattern);
                var matchingFiles = Directory.GetFiles(assetsPath, cleanPattern + "*");

                if (matchingFiles.Length > 0)
                {
                    var filename = Path.GetFileName(matchingFiles[0]);
                    return Results.Json(new { fileName = filename, url = $"/custom-sprites/{filename}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching for sprite pattern '{pattern}': {ex.Message}");
            }

            return Results.NotFound();
        })
        .WithName("SearchCustomSprite")
        .WithTags("Files")
        .Produces(200)
        .Produces(404);

        app.MapGet("/custom-sprites/{fileName}", (string fileName) =>
        {
            var assetsPath = ResolveAssetsPath();

            if (assetsPath == null)
            {
                Console.WriteLine($"❌ Assets folder not found.");
                return Results.NotFound();
            }

            Console.WriteLine($"📂 Using assets path: {assetsPath}");

            var filePath = Path.GetFullPath(Path.Combine(assetsPath, fileName));

            if (!filePath.StartsWith(Path.GetFullPath(assetsPath) + Path.DirectorySeparatorChar) &&
                !filePath.Equals(Path.GetFullPath(assetsPath)))
            {
                Console.WriteLine($"❌ Security violation: {filePath} is outside assets directory");
                return Results.BadRequest("Invalid file path");
            }

            if (File.Exists(filePath))
            {
                Console.WriteLine($"✅ Found exact file: {filePath}");
                var contentType = GetSpriteContentType(fileName);
                return Results.File(filePath, contentType);
            }

            try
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);

                Console.WriteLine($"🔍 Searching for pattern: {fileNameWithoutExtension}*{extension}");

                var matchingFiles = Directory.GetFiles(assetsPath, fileNameWithoutExtension + "*" + extension);

                Console.WriteLine($"📁 Found {matchingFiles.Length} matching files");

                if (matchingFiles.Length > 0)
                {
                    var matchedFile = matchingFiles[0];
                    Console.WriteLine($"✅ Using matched file: {matchedFile}");

                    var contentType = GetSpriteContentType(fileName);
                    return Results.File(matchedFile, contentType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error searching for file pattern: {ex.Message}");
            }

            Console.WriteLine($"❌ File not found: {fileName}");
            return Results.NotFound();
        })
        .WithName("GetCustomSprite")
        .WithTags("Files")
        .Produces(200, contentType: "image/png")
        .Produces(200, contentType: "image/webp")
        .Produces(200, contentType: "application/octet-stream")
        .Produces(400)
        .Produces(404);
    }

    private enum SpriteKind { Default, Artwork, ArtworkShiny, Shiny, Home, HomeShiny, Showdown, ShowdownShiny, Github, GithubShiny }

    private static async Task<IResult> ServeOrCachePokemonVersionSpriteAsync(
        int pokemonId,
        string gameSlug,
        string kind,
        string extension,
        AppDbContext db,
        ImageCacheService imageCacheService)
    {
        var property = kind switch
        {
            "front" => "front_default",
            "shiny" => "front_shiny",
            "back" => "back_default",
            "back-shiny" => "back_shiny",
            _ => null
        };
        if (property == null) return Results.NotFound();

        var safeGameSlug = System.Text.RegularExpressions.Regex.Replace(gameSlug, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrWhiteSpace(safeGameSlug)) return Results.NotFound();

        extension = extension.Equals("gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "png";
        var relativePath = $"pokemon/version/{pokemonId}/{safeGameSlug}/{kind}.{extension}";
        var localPath = imageCacheService.ResolveSpritePath(relativePath);
        if (localPath != null)
            return Results.File(localPath, extension == "gif" ? "image/gif" : "image/png");

        var spritesJson = await db.PokedexPokemon
            .AsNoTracking()
            .Where(p => p.PokemonId == pokemonId)
            .Select(p => p.Sprites)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(spritesJson)) return Results.NotFound();

        var externalUrl = ExtractVersionSpriteUrl(spritesJson, safeGameSlug, property);
        if (string.IsNullOrWhiteSpace(externalUrl)) return Results.NotFound();

        var downloadedRelativePath = await imageCacheService.DownloadFileAsync(externalUrl, relativePath);
        if (downloadedRelativePath == null) return Results.NotFound();

        localPath = imageCacheService.ResolveSpritePath(downloadedRelativePath);
        if (localPath == null) return Results.NotFound();

        return Results.File(localPath, extension == "gif" ? "image/gif" : "image/png");
    }

    private static string? ExtractVersionSpriteUrl(string spritesJson, string gameSlug, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            if (!doc.RootElement.TryGetProperty("versions", out var versions)) return null;

            foreach (var generation in versions.EnumerateObject())
            {
                if (!generation.Value.TryGetProperty(gameSlug, out var game)) continue;
                return TryGetString(game, property);
            }
        }
        catch { }

        return null;
    }

    /// <summary>Returns the fallback chain for a given sprite kind. The first element is the requested kind itself.
    /// Every chain is exhaustive — it tries ALL available sprite sources so we never 404 if the Pokémon exists in DB.</summary>
    private static SpriteKind[] GetFallbackChain(SpriteKind kind) => kind switch
    {
        SpriteKind.Default => new[] { SpriteKind.Default, SpriteKind.Home, SpriteKind.Artwork, SpriteKind.Github, SpriteKind.Showdown },
        SpriteKind.Shiny => new[] { SpriteKind.Shiny, SpriteKind.HomeShiny, SpriteKind.ArtworkShiny, SpriteKind.GithubShiny, SpriteKind.ShowdownShiny, SpriteKind.Default, SpriteKind.Home, SpriteKind.Artwork, SpriteKind.Github, SpriteKind.Showdown },
        SpriteKind.Artwork => new[] { SpriteKind.Artwork, SpriteKind.Home, SpriteKind.Default, SpriteKind.Github, SpriteKind.Showdown },
        SpriteKind.ArtworkShiny => new[] { SpriteKind.ArtworkShiny, SpriteKind.Artwork, SpriteKind.HomeShiny, SpriteKind.Home, SpriteKind.GithubShiny, SpriteKind.Shiny, SpriteKind.Default, SpriteKind.ShowdownShiny, SpriteKind.Showdown },
        SpriteKind.Home => new[] { SpriteKind.Home, SpriteKind.Artwork, SpriteKind.Default, SpriteKind.Github, SpriteKind.Showdown },
        SpriteKind.HomeShiny => new[] { SpriteKind.HomeShiny, SpriteKind.Home, SpriteKind.ArtworkShiny, SpriteKind.Artwork, SpriteKind.GithubShiny, SpriteKind.Shiny, SpriteKind.Default, SpriteKind.ShowdownShiny, SpriteKind.Showdown },
        SpriteKind.Showdown => new[] { SpriteKind.Showdown, SpriteKind.Home, SpriteKind.Default, SpriteKind.Github, SpriteKind.Artwork },
        SpriteKind.ShowdownShiny => new[] { SpriteKind.ShowdownShiny, SpriteKind.Showdown, SpriteKind.HomeShiny, SpriteKind.Home, SpriteKind.GithubShiny, SpriteKind.Shiny, SpriteKind.Default, SpriteKind.ArtworkShiny, SpriteKind.Artwork },
        SpriteKind.Github => new[] { SpriteKind.Github, SpriteKind.Home, SpriteKind.Default, SpriteKind.Artwork, SpriteKind.Showdown },
        SpriteKind.GithubShiny => new[] { SpriteKind.GithubShiny, SpriteKind.Github, SpriteKind.HomeShiny, SpriteKind.Home, SpriteKind.Shiny, SpriteKind.Default, SpriteKind.ArtworkShiny, SpriteKind.Artwork, SpriteKind.ShowdownShiny, SpriteKind.Showdown },
        _ => new[] { SpriteKind.Default, SpriteKind.Home, SpriteKind.Github, SpriteKind.Artwork, SpriteKind.Showdown },
    };

    private static async Task<IResult> ServeOrCachePokemonSpriteAsync(
        int pokemonId, SpriteKind kind, AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        var row = await db.PokedexPokemon
            .Where(p => p.PokemonId == pokemonId)
            .Select(p => new
            {
                p.SpriteData,
                p.ArtworkData,
                p.ArtworkShinyData,
                p.ShinyData,
                p.HomeSpriteData,
                p.HomeShinyData,
                p.ShowdownData,
                p.ShowdownShinyData,
                p.GithubSpriteData,
                p.GithubShinySpriteData,
                p.Sprites,
                p.Name
            })
            .FirstOrDefaultAsync();

        if (row == null) return Results.NotFound();

        // Try the requested kind, then each fallback in the chain
        var chain = GetFallbackChain(kind);
        foreach (var tryKind in chain)
        {
            // 1. Check DB cache first
            var cached = GetCachedBytes(tryKind, row);
            if (cached != null)
                return Results.File(cached, DetectContentType(cached));

            // 2. Try to lazy-download
            byte[]? bytes = null;
            if (tryKind == SpriteKind.Github || tryKind == SpriteKind.GithubShiny)
            {
                // Pokesprite: name-based URL with smart fallback (strip form suffixes)
                bytes = await TryDownloadPokeSpriteAsync(tryKind, row.Name, httpClientFactory);
            }
            else
            {
                var externalUrl = ExtractExternalUrl(tryKind, row.Sprites);
                if (!string.IsNullOrEmpty(externalUrl))
                {
                    try
                    {
                        var client = httpClientFactory.CreateClient("PokeApi");
                        bytes = await client.GetByteArrayAsync(externalUrl);
                    }
                    catch { /* download failed, try next fallback */ }
                }
            }

            if (bytes != null)
            {
                await StoreSpriteBytes(tryKind, pokemonId, bytes, db);
                return Results.File(bytes, DetectContentType(bytes));
            }
        }

        return Results.NotFound();
    }

    /// <summary>
    /// Tries to download a pokesprite from msikma/pokesprite with smart name fallback.
    /// If "aegislash-shield" 404s, tries "aegislash". If "charizard-mega-x" works, returns it directly.
    /// </summary>
    private static async Task<byte[]?> TryDownloadPokeSpriteAsync(
        SpriteKind kind, string pokemonName, IHttpClientFactory httpClientFactory)
    {
        var variant = kind == SpriteKind.GithubShiny ? "shiny" : "regular";
        var client = httpClientFactory.CreateClient();
        var name = pokemonName;

        while (!string.IsNullOrEmpty(name))
        {
            var url = $"https://raw.githubusercontent.com/msikma/pokesprite/master/pokemon-gen8/{variant}/{name}.png";
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync();
            }
            catch { /* network error, try shorter name */ }

            // Strip last hyphenated segment: "aegislash-shield" → "aegislash"
            var lastHyphen = name.LastIndexOf('-');
            if (lastHyphen <= 0) break;
            name = name[..lastHyphen];
        }

        return null;
    }

    private static byte[]? GetCachedBytes(SpriteKind kind, dynamic row) => kind switch
    {
        SpriteKind.Default => row.SpriteData,
        SpriteKind.Shiny => row.ShinyData,
        SpriteKind.Artwork => row.ArtworkData,
        SpriteKind.ArtworkShiny => row.ArtworkShinyData,
        SpriteKind.Home => row.HomeSpriteData,
        SpriteKind.HomeShiny => row.HomeShinyData,
        SpriteKind.Showdown => row.ShowdownData,
        SpriteKind.ShowdownShiny => row.ShowdownShinyData,
        SpriteKind.Github => row.GithubSpriteData,
        SpriteKind.GithubShiny => row.GithubShinySpriteData,
        _ => null,
    };

    private static string? ExtractExternalUrl(SpriteKind kind, string spritesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;

            return kind switch
            {
                SpriteKind.Default => TryGetString(root, "front_default"),
                SpriteKind.Shiny => TryGetString(root, "front_shiny"),
                SpriteKind.Artwork => TryGetNested(root, "other", "official-artwork", "front_default"),
                SpriteKind.ArtworkShiny => TryGetNested(root, "other", "official-artwork", "front_shiny"),
                SpriteKind.Home => TryGetNested(root, "other", "home", "front_default"),
                SpriteKind.HomeShiny => TryGetNested(root, "other", "home", "front_shiny"),
                SpriteKind.Showdown => TryGetNested(root, "other", "showdown", "front_default"),
                SpriteKind.ShowdownShiny => TryGetNested(root, "other", "showdown", "front_shiny"),
                _ => null,
            };
        }
        catch { return null; }
    }

    private static string? TryGetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? TryGetNested(JsonElement root, string a, string b, string c)
        => root.TryGetProperty(a, out var lvl1) && lvl1.TryGetProperty(b, out var lvl2) ? TryGetString(lvl2, c) : null;

    private static async Task StoreSpriteBytes(SpriteKind kind, int pokemonId, byte[] bytes, AppDbContext db)
    {
        switch (kind)
        {
            case SpriteKind.Default:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.SpriteData, bytes));
                break;
            case SpriteKind.Shiny:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShinyData, bytes));
                break;
            case SpriteKind.Artwork:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ArtworkData, bytes));
                break;
            case SpriteKind.ArtworkShiny:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ArtworkShinyData, bytes));
                break;
            case SpriteKind.Home:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.HomeSpriteData, bytes));
                break;
            case SpriteKind.HomeShiny:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.HomeShinyData, bytes));
                break;
            case SpriteKind.Showdown:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShowdownData, bytes));
                break;
            case SpriteKind.ShowdownShiny:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.ShowdownShinyData, bytes));
                break;
            case SpriteKind.Github:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.GithubSpriteData, bytes));
                break;
            case SpriteKind.GithubShiny:
                await db.PokedexPokemon.Where(p => p.PokemonId == pokemonId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.GithubShinySpriteData, bytes));
                break;
        }
    }

    /// <summary>Detects image content type from magic bytes (PNG vs GIF).</summary>
    private static string DetectContentType(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";
        return "image/png";
    }

    private static string? ResolveAssetsPath()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "assets"),
            Path.Combine(AppContext.BaseDirectory, "assets"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
        };

        var envAssetsPath = Environment.GetEnvironmentVariable("BEASTVAULT_ASSETS_PATH");
        if (!string.IsNullOrEmpty(envAssetsPath))
            possiblePaths.Insert(0, envAssetsPath);

        var parentDir = Directory.GetParent(AppContext.BaseDirectory)?.FullName;
        if (parentDir != null)
            possiblePaths.Add(Path.Combine(parentDir, "assets"));

        possiblePaths = possiblePaths.Distinct().ToList();

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    private static string GetSpriteContentType(string fileName)
    {
        return fileName.EndsWith(".png") ? "image/png" :
               fileName.EndsWith(".webp") ? "image/webp" :
               "application/octet-stream";
    }
}
