using System.Text.Json;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

/// <summary>
/// Downloads Pokémon and item sprites from PokéAPI and stores them locally so the
/// application can run fully offline. Sprites are saved under the configured
/// SpritesPath directory and served via GET /sprites/{**path}.
/// </summary>
public class ImageCacheService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly string _spritesRoot;

    // Progress tracking (static so fire-and-forget tasks can update it)
    private static volatile bool _isDownloading;
    private static int _downloadCurrent;
    private static int _downloadTotal;
    private static readonly object _downloadLock = new();

    public static bool IsDownloading => _isDownloading;
    public static int DownloadCurrent => _downloadCurrent;
    public static int DownloadTotal => _downloadTotal;

    public ImageCacheService(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("PokeApi");
        _spritesRoot = configuration["SpritesPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "sprites");
    }

    public string SpritesRoot => _spritesRoot;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Downloads sprites for a single Pokémon and stores them as bytes in DB (used during populate).</summary>
    public async Task DownloadSpritesForPokemonAsync(PokedexPokemon pokemon)
    {
        try
        {
            var (spriteUrl, artworkUrl) = ExtractSpriteUrls(pokemon.Sprites);
            var shinyUrl = ExtractShinyUrl(pokemon.Sprites);
            var (homeUrl, homeShinyUrl) = ExtractHomeUrls(pokemon.Sprites);
            var artworkShinyUrl = ExtractArtworkShinyUrl(pokemon.Sprites);
            var (showdownUrl, showdownShinyUrl) = ExtractShowdownUrls(pokemon.Sprites);

            if (!string.IsNullOrEmpty(spriteUrl) && pokemon.SpriteData == null)
                pokemon.SpriteData = await SafeDownloadAsync(spriteUrl);

            if (!string.IsNullOrEmpty(artworkUrl) && pokemon.ArtworkData == null)
                pokemon.ArtworkData = await SafeDownloadAsync(artworkUrl);

            if (!string.IsNullOrEmpty(artworkShinyUrl) && pokemon.ArtworkShinyData == null)
                pokemon.ArtworkShinyData = await SafeDownloadAsync(artworkShinyUrl);

            if (!string.IsNullOrEmpty(shinyUrl) && pokemon.ShinyData == null)
                pokemon.ShinyData = await SafeDownloadAsync(shinyUrl);

            if (!string.IsNullOrEmpty(homeUrl) && pokemon.HomeSpriteData == null)
                pokemon.HomeSpriteData = await SafeDownloadAsync(homeUrl);

            if (!string.IsNullOrEmpty(homeShinyUrl) && pokemon.HomeShinyData == null)
                pokemon.HomeShinyData = await SafeDownloadAsync(homeShinyUrl);

            if (!string.IsNullOrEmpty(showdownUrl) && pokemon.ShowdownData == null)
                pokemon.ShowdownData = await SafeDownloadAsync(showdownUrl);

            if (!string.IsNullOrEmpty(showdownShinyUrl) && pokemon.ShowdownShinyData == null)
                pokemon.ShowdownShinyData = await SafeDownloadAsync(showdownShinyUrl);

            // Pokesprite (msikma/pokesprite) — name-based URL with smart form fallback
            if (pokemon.GithubSpriteData == null)
                pokemon.GithubSpriteData = await DownloadPokeSpriteAsync(pokemon.Name, shiny: false);

            if (pokemon.GithubShinySpriteData == null)
                pokemon.GithubShinySpriteData = await DownloadPokeSpriteAsync(pokemon.Name, shiny: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageCache] Failed to download sprites for pokemon {pokemon.PokemonId}: {ex.Message}");
        }
    }

    /// <summary>Downloads bytes from a URL, returning null on failure instead of throwing.</summary>
    private async Task<byte[]?> SafeDownloadAsync(string url)
    {
        try { return await _httpClient.GetByteArrayAsync(url); }
        catch { return null; }
    }

    /// <summary>
    /// Downloads a pokesprite from msikma/pokesprite with smart name fallback.
    /// Tries exact name first, then progressively strips the last hyphenated segment.
    /// e.g. "aegislash-shield" → 404 → "aegislash" → success.
    /// </summary>
    private async Task<byte[]?> DownloadPokeSpriteAsync(string pokemonName, bool shiny)
    {
        var variant = shiny ? "shiny" : "regular";
        var name = pokemonName;

        while (!string.IsNullOrEmpty(name))
        {
            var url = $"https://raw.githubusercontent.com/msikma/pokesprite/master/pokemon-gen8/{variant}/{name}.png";
            var bytes = await SafeDownloadAsync(url);
            if (bytes != null) return bytes;

            // Strip last hyphenated segment: "pikachu-original-cap" → "pikachu-original" → "pikachu"
            var lastHyphen = name.LastIndexOf('-');
            if (lastHyphen <= 0) break;
            name = name[..lastHyphen];
        }

        return null;
    }

    /// <summary>Downloads all pending Pokémon sprites (default + artwork) and item sprites.</summary>
    public async Task<SpriteDownloadResult> DownloadAllSpritesAsync()
    {
        lock (_downloadLock)
        {
            if (_isDownloading) return new SpriteDownloadResult(false, 0, 0, 0, 0, "Already downloading");
            _isDownloading = true;
            _downloadCurrent = 0;
            _downloadTotal = 0;
        }

        var result = new SpriteDownloadResult(true, 0, 0, 0, 0, null);

        try
        {

            // Load only IDs + Sprites JSON (no blobs) for Pokémon that lack any sprite bytes
            var pokemonToSync = await _context.PokedexPokemon
                .Where(p => p.SpriteData == null || p.HomeSpriteData == null
                         || p.ShowdownData == null || p.ArtworkShinyData == null
                         || p.GithubSpriteData == null)
                .Select(p => new
                {
                    p.PokemonId,
                    p.Name,
                    p.Sprites,
                    p.SpriteData,
                    p.ArtworkData,
                    p.ArtworkShinyData,
                    p.ShinyData,
                    p.HomeSpriteData,
                    p.HomeShinyData,
                    p.ShowdownData,
                    p.ShowdownShinyData,
                    p.GithubSpriteData,
                    p.GithubShinySpriteData
                })
                .ToListAsync();

            var itemEntries = await _context.PokedexItems
                .Where(i => i.SpriteLocalPath == null && i.SpriteUrl != "")
                .ToListAsync();

            _downloadTotal = pokemonToSync.Count + itemEntries.Count;

            int pokemonOk = 0, pokemonFail = 0, itemOk = 0, itemFail = 0;

            foreach (var item in pokemonToSync)
            {
                _downloadCurrent++;
                bool ok = false;
                try
                {
                    var (spriteUrl, artworkUrl) = ExtractSpriteUrls(item.Sprites);
                    var shinyUrl = ExtractShinyUrl(item.Sprites);
                    var (homeUrl, homeShinyUrl) = ExtractHomeUrls(item.Sprites);
                    var artworkShinyUrl = ExtractArtworkShinyUrl(item.Sprites);
                    var (showdownUrl, showdownShinyUrl) = ExtractShowdownUrls(item.Sprites);

                    var pid = item.PokemonId;

                    if (!string.IsNullOrEmpty(spriteUrl) && item.SpriteData == null)
                    { var b = await SafeDownloadAsync(spriteUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.SpriteData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(artworkUrl) && item.ArtworkData == null)
                    { var b = await SafeDownloadAsync(artworkUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.ArtworkData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(artworkShinyUrl) && item.ArtworkShinyData == null)
                    { var b = await SafeDownloadAsync(artworkShinyUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.ArtworkShinyData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(shinyUrl) && item.ShinyData == null)
                    { var b = await SafeDownloadAsync(shinyUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.ShinyData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(homeUrl) && item.HomeSpriteData == null)
                    { var b = await SafeDownloadAsync(homeUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.HomeSpriteData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(homeShinyUrl) && item.HomeShinyData == null)
                    { var b = await SafeDownloadAsync(homeShinyUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.HomeShinyData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(showdownUrl) && item.ShowdownData == null)
                    { var b = await SafeDownloadAsync(showdownUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.ShowdownData, b)); ok = true; } }

                    if (!string.IsNullOrEmpty(showdownShinyUrl) && item.ShowdownShinyData == null)
                    { var b = await SafeDownloadAsync(showdownShinyUrl); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.ShowdownShinyData, b)); ok = true; } }

                    // Pokesprite (msikma/pokesprite) — name-based with form fallback
                    if (item.GithubSpriteData == null)
                    { var b = await DownloadPokeSpriteAsync(item.Name, shiny: false); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.GithubSpriteData, b)); ok = true; } }

                    if (item.GithubShinySpriteData == null)
                    { var b = await DownloadPokeSpriteAsync(item.Name, shiny: true); if (b != null) { await _context.PokedexPokemon.Where(p => p.PokemonId == pid).ExecuteUpdateAsync(s => s.SetProperty(p => p.GithubShinySpriteData, b)); ok = true; } }

                    if (ok) pokemonOk++; else pokemonFail++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ImageCache] Error downloading sprites for pokemon {item.PokemonId}: {ex.Message}");
                    pokemonFail++;
                }

                await Task.Delay(80); // gentle rate-limit
            }

            foreach (var item in itemEntries)
            {
                _downloadCurrent++;
                try
                {
                    var localPath = await DownloadFileAsync(item.SpriteUrl, Path.Combine("items", $"{item.ItemId}.png"));
                    if (localPath != null)
                    {
                        item.SpriteLocalPath = localPath;
                        itemOk++;
                    }
                    else
                    {
                        itemFail++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ImageCache] Error downloading sprite for item {item.ItemId}: {ex.Message}");
                    itemFail++;
                }

                if (_downloadCurrent % 50 == 0)
                    await _context.SaveChangesAsync();

                await Task.Delay(50);
            }

            await _context.SaveChangesAsync();

            result = new SpriteDownloadResult(true, pokemonOk, pokemonFail, itemOk, itemFail, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageCache] Fatal error: {ex.Message}");
            result = result with { Error = ex.Message };
        }
        finally
        {
            _isDownloading = false;
            _downloadCurrent = 0;
            _downloadTotal = 0;
        }

        return result;
    }

    /// <summary>Resolves the local file path for a sprite to serve via HTTP.</summary>
    public string? ResolveSpritePath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_spritesRoot, relativePath));
        if (!full.StartsWith(Path.GetFullPath(_spritesRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return null; // path traversal guard

        return File.Exists(full) ? full : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_spritesRoot);
        Directory.CreateDirectory(Path.Combine(_spritesRoot, "pokemon"));
        Directory.CreateDirectory(Path.Combine(_spritesRoot, "pokemon", "artwork"));
        Directory.CreateDirectory(Path.Combine(_spritesRoot, "pokemon", "shiny"));
        Directory.CreateDirectory(Path.Combine(_spritesRoot, "items"));
    }

    /// <summary>
    /// Downloads a remote URL to a local file inside <see cref="SpritesRoot"/>.
    /// Returns the relative path (e.g. "pokemon/25.png") or null on failure.
    /// </summary>
    private async Task<string?> DownloadFileAsync(string url, string relativePath)
    {
        try
        {
            var dest = Path.Combine(_spritesRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            // Skip already downloaded files
            if (File.Exists(dest)) return relativePath;

            var bytes = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(dest, bytes);
            return relativePath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageCache] Failed to download {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the raw Sprites JSON from PokéAPI and extracts the front_default
    /// and official-artwork URLs.
    /// </summary>
    private static string? ExtractShinyUrl(string spritesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("front_shiny", out var fs) && fs.ValueKind == JsonValueKind.String)
                return fs.GetString();
            return null;
        }
        catch { return null; }
    }

    private static (string? home, string? homeShiny) ExtractHomeUrls(string spritesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;
            string? home = null;
            string? homeShiny = null;
            if (root.TryGetProperty("other", out var other) && other.TryGetProperty("home", out var h))
            {
                if (h.TryGetProperty("front_default", out var hfd) && hfd.ValueKind == JsonValueKind.String)
                    home = hfd.GetString();
                if (h.TryGetProperty("front_shiny", out var hfs) && hfs.ValueKind == JsonValueKind.String)
                    homeShiny = hfs.GetString();
            }
            return (home, homeShiny);
        }
        catch { return (null, null); }
    }

    private static string? ExtractArtworkShinyUrl(string spritesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("other", out var other) &&
                other.TryGetProperty("official-artwork", out var oa) &&
                oa.TryGetProperty("front_shiny", out var fs) &&
                fs.ValueKind == JsonValueKind.String)
                return fs.GetString();
            return null;
        }
        catch { return null; }
    }

    private static (string? showdown, string? showdownShiny) ExtractShowdownUrls(string spritesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;
            string? showdown = null;
            string? showdownShiny = null;
            if (root.TryGetProperty("other", out var other) && other.TryGetProperty("showdown", out var sd))
            {
                if (sd.TryGetProperty("front_default", out var sfd) && sfd.ValueKind == JsonValueKind.String)
                    showdown = sfd.GetString();
                if (sd.TryGetProperty("front_shiny", out var sfs) && sfs.ValueKind == JsonValueKind.String)
                    showdownShiny = sfs.GetString();
            }
            return (showdown, showdownShiny);
        }
        catch { return (null, null); }
    }

    private static (string? sprite, string? artwork) ExtractSpriteUrls(string spritesJson)
    {
        try
        {
            var doc = JsonDocument.Parse(spritesJson);
            var root = doc.RootElement;

            string? sprite = null;
            if (root.TryGetProperty("front_default", out var fd) && fd.ValueKind == JsonValueKind.String)
                sprite = fd.GetString();

            string? artwork = null;
            if (root.TryGetProperty("other", out var other))
            {
                if (other.TryGetProperty("official-artwork", out var oa))
                {
                    if (oa.TryGetProperty("front_default", out var oafd) && oafd.ValueKind == JsonValueKind.String)
                        artwork = oafd.GetString();
                }
            }

            return (sprite, artwork);
        }
        catch
        {
            return (null, null);
        }
    }
}

public record SpriteDownloadResult(
    bool Started,
    int PokemonOk,
    int PokemonFailed,
    int ItemOk,
    int ItemFailed,
    string? Error
);

public record SpriteDownloadStatusResponse(
    bool IsDownloading,
    int Current,
    int Total,
    int SpritesOnDisk,
    int ArtworkOnDisk,
    int ItemSpritesOnDisk
);
