using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public interface IBulbapediaService
{
    Task<BulbapediaCache?> FetchAndCachePageAsync(int speciesId, string pageName);
    Task<BulbapediaNormalizeResult> NormalizeSpeciesAsync(int speciesId, bool force = false);
    Task<BulbapediaNormalizeRangeResult> NormalizeSpeciesRangeAsync(int startId, int endId, bool force = false);
    Task<int> EnrichSpeciesRangeAsync(int startId, int endId);
}

public record BulbapediaNormalizeResult(
    int SpeciesId,
    bool Success,
    int Entries,
    int Locations,
    int Sprites,
    string? Error
);

public record BulbapediaNormalizeRangeResult(
    int Enriched,
    int Normalized,
    int Failed,
    int Entries,
    int Locations,
    int Sprites
);

internal sealed record ParsedFlavor(string Game, string Text);
internal sealed record ParsedLocation(string Game, string Location, string? Method);
internal sealed record ParsedSprite(
    int Generation,
    string GameSlug,
    string DisplayLabel,
    string? NormalUrl,
    string? BackUrl,
    int SortOrder
);

public class BulbapediaService : IBulbapediaService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ImageCacheService _imageCache;
    private readonly ILogger<BulbapediaService> _logger;
    private const string BaseUrl = "https://bulbapedia.bulbagarden.net/w/api.php";

    private static readonly Dictionary<string, int> RomanGeneration = new(StringComparer.OrdinalIgnoreCase)
    {
        ["I"] = 1, ["II"] = 2, ["III"] = 3, ["IV"] = 4, ["V"] = 5,
        ["VI"] = 6, ["VII"] = 7, ["VIII"] = 8, ["IX"] = 9, ["X"] = 10
    };

    private static readonly Dictionary<string, (string Slug, int Generation, int Sort)> GameInfo =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Red"] = ("red", 1, 10), ["Green"] = ("green", 1, 11), ["Blue"] = ("blue", 1, 12),
            ["Yellow"] = ("yellow", 1, 13), ["Stadium"] = ("stadium", 1, 14),
            ["Gold"] = ("gold", 2, 20), ["Silver"] = ("silver", 2, 21), ["Crystal"] = ("crystal", 2, 22),
            ["Stadium 2"] = ("stadium-2", 2, 23),
            ["Ruby"] = ("ruby", 3, 30), ["Sapphire"] = ("sapphire", 3, 31), ["Emerald"] = ("emerald", 3, 32),
            ["FireRed"] = ("firered", 3, 33), ["LeafGreen"] = ("leafgreen", 3, 34),
            ["Diamond"] = ("diamond", 4, 40), ["Pearl"] = ("pearl", 4, 41), ["Platinum"] = ("platinum", 4, 42),
            ["HeartGold"] = ("heartgold", 4, 43), ["SoulSilver"] = ("soulsilver", 4, 44),
            ["Black"] = ("black", 5, 50), ["White"] = ("white", 5, 51), ["Black 2"] = ("black-2", 5, 52), ["White 2"] = ("white-2", 5, 53),
            ["X"] = ("x", 6, 60), ["Y"] = ("y", 6, 61), ["Omega Ruby"] = ("omega-ruby", 6, 62), ["Alpha Sapphire"] = ("alpha-sapphire", 6, 63),
            ["Sun"] = ("sun", 7, 70), ["Moon"] = ("moon", 7, 71), ["Ultra Sun"] = ("ultra-sun", 7, 72), ["Ultra Moon"] = ("ultra-moon", 7, 73),
            ["Let's Go Pikachu"] = ("lets-go-pikachu", 7, 74), ["Let's Go Eevee"] = ("lets-go-eevee", 7, 75),
            ["Sword"] = ("sword", 8, 80), ["Shield"] = ("shield", 8, 81),
            ["Brilliant Diamond"] = ("brilliant-diamond", 8, 82), ["Shining Pearl"] = ("shining-pearl", 8, 83), ["Legends: Arceus"] = ("legends-arceus", 8, 84),
            ["Scarlet"] = ("scarlet", 9, 90), ["Violet"] = ("violet", 9, 91), ["Legends: Z-A"] = ("legends-za", 9, 92), ["Pokopia"] = ("pokopia", 9, 93),
            ["Mega Dimension"] = ("mega-dimension", 9, 94), ["Expansion Pass"] = ("sword-shield-expansion-pass", 8, 85), ["Pal Park"] = ("pal-park", 4, 45)
        };

    public BulbapediaService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ImageCacheService imageCache,
        ILogger<BulbapediaService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _imageCache = imageCache;
        _logger = logger;
    }

    public async Task<BulbapediaCache?> FetchAndCachePageAsync(int speciesId, string pageName)
    {
        var existing = await _db.BulbapediaCache.FirstOrDefaultAsync(c => c.SpeciesId == speciesId);
        var client = _httpClientFactory.CreateClient("Bulbapedia");
        var url = $"{BaseUrl}?action=parse&format=json&page={Uri.EscapeDataString(pageName)}&prop=wikitext|text|sections|revid";

        try
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return await SaveCacheEntry(existing, speciesId, pageName, null, null, ParseStatus.Failed, $"HTTP {(int)response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errorMsg = error.TryGetProperty("info", out var info) ? info.GetString() : "Unknown error";
                return await SaveCacheEntry(existing, speciesId, pageName, null, null, ParseStatus.Failed, errorMsg);
            }

            var parse = doc.RootElement.GetProperty("parse");
            var revId = parse.TryGetProperty("revid", out var rev) ? rev.GetInt32() : (int?)null;
            var pageId = parse.TryGetProperty("pageid", out var pid) ? pid.GetInt32() : (int?)null;
            var wikitext = parse.TryGetProperty("wikitext", out var wt) && wt.TryGetProperty("*", out var wtStar) ? wtStar.GetString() : null;
            var html = parse.TryGetProperty("text", out var text) && text.TryGetProperty("*", out var htmlStar) ? htmlStar.GetString() : null;
            var sections = parse.TryGetProperty("sections", out var sec) ? sec.GetRawText() : null;

            var entry = await SaveCacheEntry(existing, speciesId, pageName, wikitext, html, ParseStatus.Success, null);
            entry.RevisionId = revId;
            entry.PageId = pageId;
            entry.ParsedSections = sections;
            await _db.SaveChangesAsync();
            return entry;
        }
        catch (Exception ex)
        {
            return await SaveCacheEntry(existing, speciesId, pageName, null, null, ParseStatus.Failed, ex.Message);
        }
    }

    public async Task<int> EnrichSpeciesRangeAsync(int startId, int endId)
    {
        var result = await NormalizeSpeciesRangeAsync(startId, endId, force: true);
        return result.Enriched;
    }

    public async Task<BulbapediaNormalizeRangeResult> NormalizeSpeciesRangeAsync(int startId, int endId, bool force = false)
    {
        var enriched = 0;
        var normalized = 0;
        var failed = 0;
        var entries = 0;
        var locations = 0;
        var sprites = 0;

        _logger.LogInformation("Bulbapedia enrichment + normalization starting for species {StartId}-{EndId}", startId, endId);

        for (var speciesId = startId; speciesId <= endId; speciesId++)
        {
            var species = await _db.PokedexEntries.FindAsync(speciesId);
            if (species == null) continue;

            var cache = await _db.BulbapediaCache.FirstOrDefaultAsync(c => c.SpeciesId == speciesId);
            if (cache == null || force || cache.Status != ParseStatus.Success || string.IsNullOrWhiteSpace(cache.RawHtml))
            {
                var pageName = BuildPageName(species.Name);
                cache = await FetchAndCachePageAsync(speciesId, pageName);
                if (cache?.Status == ParseStatus.Success) enriched++;
                await Task.Delay(1000);
            }

            var result = await NormalizeSpeciesAsync(speciesId, force);
            if (result.Success)
            {
                normalized++;
                entries += result.Entries;
                locations += result.Locations;
                sprites += result.Sprites;
            }
            else
            {
                failed++;
                _logger.LogWarning("Bulbapedia normalization failed for species {SpeciesId}: {Error}", speciesId, result.Error);
            }
        }

        return new BulbapediaNormalizeRangeResult(enriched, normalized, failed, entries, locations, sprites);
    }

    public async Task<BulbapediaNormalizeResult> NormalizeSpeciesAsync(int speciesId, bool force = false)
    {
        var cache = await _db.BulbapediaCache.FirstOrDefaultAsync(c => c.SpeciesId == speciesId);
        if (cache == null || cache.Status != ParseStatus.Success)
            return new BulbapediaNormalizeResult(speciesId, false, 0, 0, 0, "Bulbapedia page is not cached successfully.");

        if (!force && cache.NormalizedStatus == ParseStatus.Success && cache.NormalizedAt.HasValue)
            return new BulbapediaNormalizeResult(speciesId, true, cache.EntriesCount, cache.LocationsCount, cache.SpritesCount, null);

        try
        {
            var raw = cache.RawContent ?? "";
            var html = cache.RawHtml ?? "";
            var entries = ParseFlavorEntries(raw);
            var locations = ParseLocations(raw);
            var nameMeaning = ParseNameMeaning(raw, html);
            var sprites = await ParseAndCacheSpritesAsync(speciesId, html);

            await _db.PokedexFlavorEntries
                .Where(e => e.SpeciesId == speciesId && e.Source == CacheSource.Bulbapedia)
                .ExecuteDeleteAsync();
            await _db.PokedexLocations
                .Where(l => l.SpeciesId == speciesId && l.Source == CacheSource.Bulbapedia)
                .ExecuteDeleteAsync();
            await _db.PokedexSpriteEntries
                .Where(s => s.SpeciesId == speciesId && s.Source == CacheSource.Bulbapedia)
                .ExecuteDeleteAsync();

            foreach (var entry in Deduplicate(entries, e => $"{NormalizeGameSlug(e.Game)}|{e.Text}"))
            {
                _db.PokedexFlavorEntries.Add(new PokedexFlavorEntry
                {
                    SpeciesId = speciesId,
                    Language = "en",
                    GameVersion = NormalizeGameSlug(entry.Game),
                    Text = entry.Text,
                    Source = CacheSource.Bulbapedia,
                    CachedAt = DateTime.UtcNow
                });
            }

            foreach (var location in Deduplicate(locations, l => $"{NormalizeGameSlug(l.Game)}|{l.Location}|{l.Method}"))
            {
                _db.PokedexLocations.Add(new PokedexLocation
                {
                    SpeciesId = speciesId,
                    Game = NormalizeGameSlug(location.Game),
                    Location = location.Location,
                    Method = location.Method,
                    Source = CacheSource.Bulbapedia,
                    CachedAt = DateTime.UtcNow
                });
            }

            foreach (var sprite in sprites)
            {
                _db.PokedexSpriteEntries.Add(new PokedexSpriteEntry
                {
                    SpeciesId = speciesId,
                    Generation = sprite.Generation,
                    GameSlug = sprite.GameSlug,
                    DisplayLabel = sprite.DisplayLabel,
                    NormalLocalPath = sprite.NormalUrl,
                    BackLocalPath = sprite.BackUrl,
                    SourceUrl = sprite.NormalUrl,
                    Source = CacheSource.Bulbapedia,
                    SortOrder = sprite.SortOrder,
                    CachedAt = DateTime.UtcNow
                });
            }

            cache.NameMeaning = nameMeaning;
            cache.NormalizedAt = DateTime.UtcNow;
            cache.NormalizedStatus = entries.Count > 0 || locations.Count > 0 || sprites.Count > 0 ? ParseStatus.Success : ParseStatus.PartialSuccess;
            cache.NormalizedError = null;
            cache.EntriesCount = entries.Count;
            cache.LocationsCount = locations.Count;
            cache.SpritesCount = sprites.Count;

            await _db.SaveChangesAsync();
            return new BulbapediaNormalizeResult(speciesId, true, entries.Count, locations.Count, sprites.Count, null);
        }
        catch (Exception ex)
        {
            cache.NormalizedAt = DateTime.UtcNow;
            cache.NormalizedStatus = ParseStatus.Failed;
            cache.NormalizedError = ex.Message;
            await _db.SaveChangesAsync();
            return new BulbapediaNormalizeResult(speciesId, false, 0, 0, 0, ex.Message);
        }
    }

    private async Task<BulbapediaCache> SaveCacheEntry(
        BulbapediaCache? existing,
        int speciesId,
        string pageName,
        string? rawContent,
        string? rawHtml,
        ParseStatus status,
        string? error)
    {
        if (existing != null)
        {
            existing.RawContent = rawContent;
            existing.RawHtml = rawHtml;
            existing.Status = status;
            existing.ErrorMessage = error;
            existing.CachedAt = DateTime.UtcNow;
            if (status == ParseStatus.Success)
            {
                existing.NormalizedStatus = ParseStatus.Pending;
                existing.NormalizedError = null;
            }
            await _db.SaveChangesAsync();
            return existing;
        }

        var entry = new BulbapediaCache
        {
            SpeciesId = speciesId,
            PageTitle = pageName,
            PageUrl = $"https://bulbapedia.bulbagarden.net/wiki/{Uri.EscapeDataString(pageName)}",
            RawContent = rawContent,
            RawHtml = rawHtml,
            Status = status,
            ErrorMessage = error,
            CachedAt = DateTime.UtcNow
        };
        _db.BulbapediaCache.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private static string BuildPageName(string speciesName)
    {
        var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(speciesName.Replace("-", " ").ToLowerInvariant()).Replace(" ", "_");
        return $"{name}_(Pokémon)";
    }

    private static List<ParsedFlavor> ParseFlavorEntries(string wikitext)
    {
        var section = ExtractSection(wikitext, "Pokédex entries");
        var results = new List<ParsedFlavor>();
        if (string.IsNullOrWhiteSpace(section)) return results;

        foreach (var template in ExtractTemplates(section, "Dex/Entry"))
        {
            var parameters = ParseTemplateParameters(template);
            if (!parameters.TryGetValue("entry", out var text)) continue;
            text = CleanWikiText(text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            foreach (var game in ExtractGames(parameters))
                results.Add(new ParsedFlavor(game, text));
        }

        foreach (var template in ExtractTemplates(section, "Dex/NE"))
        {
            var parameters = ParseTemplateParameters(template);
            var note = parameters.GetValueOrDefault("1") ?? "No Pokédex entry.";
            var text = $"No Pokédex entry in {CleanWikiText(note)}.";
            results.Add(new ParsedFlavor("No Entry", text));
        }

        return results;
    }

    private static List<ParsedLocation> ParseLocations(string wikitext)
    {
        var section = ExtractSection(wikitext, "Game locations");
        var results = new List<ParsedLocation>();
        if (string.IsNullOrWhiteSpace(section)) return results;

        foreach (var template in ExtractTemplates(section, "Availability/Entry"))
        {
            var parameters = ParseTemplateParameters(template);
            var area = parameters.GetValueOrDefault("area");
            var method = CleanWikiText(area ?? (template.Contains("/None", StringComparison.OrdinalIgnoreCase) ? "Unobtainable" : ""));
            if (string.IsNullOrWhiteSpace(method)) method = template.Contains("/None", StringComparison.OrdinalIgnoreCase) ? "Unobtainable" : null;

            foreach (var game in ExtractGames(parameters))
                results.Add(new ParsedLocation(game, method ?? "Unobtainable", template.Contains("/None", StringComparison.OrdinalIgnoreCase) ? "Unavailable" : null));
        }

        return results;
    }

    private async Task<List<ParsedSprite>> ParseAndCacheSpritesAsync(int speciesId, string html)
    {
        var results = new List<ParsedSprite>();
        if (string.IsNullOrWhiteSpace(html)) return results;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var spritesHeader = doc.DocumentNode.SelectSingleNode("//*[@id='Sprites']");
        if (spritesHeader == null) return results;

        var generation = 0;
        var order = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var node = spritesHeader.ParentNode?.NextSibling; node != null; node = node.NextSibling)
        {
            if (node.Name is "h2") break;
            var text = CleanHtmlText(node.InnerText);
            var genMatch = Regex.Match(text, @"Generation\s+([IVX]+)", RegexOptions.IgnoreCase);
            if (genMatch.Success && RomanGeneration.TryGetValue(genMatch.Groups[1].Value, out var parsedGen))
                generation = parsedGen;

            foreach (var img in node.SelectNodes(".//img[contains(@src,'/Spr_') or contains(@src,'/Menu_') or contains(@src,'/HOME_')]") ?? Enumerable.Empty<HtmlNode>())
            {
                var source = img.GetAttributeValue("src", "");
                if (string.IsNullOrWhiteSpace(source)) continue;
                if (source.StartsWith("//")) source = "https:" + source;
                if (!source.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                var fileName = Path.GetFileName(new Uri(source).AbsolutePath);
                var inferred = InferSpriteLabel(fileName, generation);
                if (inferred == null) continue;

                var key = $"{inferred.Value.Slug}|{source}";
                if (!seen.Add(key)) continue;

                var relative = await _imageCache.DownloadFileAsync(source, $"bulbapedia/{speciesId}/{SanitizeFileName(fileName)}");
                if (relative == null) continue;

                var localUrl = $"/sprites/{relative.Replace('\\', '/')}";
                var existing = results.FirstOrDefault(r => r.GameSlug == inferred.Value.Slug && r.Generation == inferred.Value.Generation);
                if (existing == null)
                {
                    results.Add(new ParsedSprite(inferred.Value.Generation, inferred.Value.Slug, inferred.Value.Label, localUrl, null, inferred.Value.Sort + order++));
                }
                else if (existing.BackUrl == null && fileName.Contains("_b_", StringComparison.OrdinalIgnoreCase))
                {
                    results.Remove(existing);
                    results.Add(existing with { BackUrl = localUrl });
                }
            }
        }

        return results
            .GroupBy(s => $"{s.Generation}|{s.GameSlug}")
            .Select(g => g.OrderBy(s => s.SortOrder).First())
            .OrderBy(s => s.SortOrder)
            .ToList();
    }

    private static string? ParseNameMeaning(string wikitext, string html)
    {
        var section = ExtractSection(wikitext, "Name origin");
        if (string.IsNullOrWhiteSpace(section)) return null;
        var cleaned = CleanWikiText(Regex.Replace(section, @"^=+\s*Name origin\s*=+", "", RegexOptions.IgnoreCase).Trim());
        var idx = cleaned.IndexOf("In other languages", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) cleaned = cleaned[..idx].Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static IEnumerable<string> ExtractGames(Dictionary<string, string> parameters)
    {
        for (var i = 1; i <= 6; i++)
        {
            var key = i == 1 ? "v" : $"v{i}";
            if (parameters.TryGetValue(key, out var game) && !string.IsNullOrWhiteSpace(game))
                yield return CleanWikiText(game);
        }
    }

    private static string ExtractSection(string text, string heading)
    {
        var match = Regex.Match(text, @$"(?im)^=+\s*{Regex.Escape(heading)}\s*=+\s*$");
        if (!match.Success) return "";
        var restStart = match.Index + match.Length;
        var next = Regex.Match(text[restStart..], @"(?m)^==[^=].*==\s*$");
        return next.Success ? text.Substring(match.Index, match.Length + next.Index) : text[match.Index..];
    }

    private static List<string> ExtractTemplates(string text, string prefix)
    {
        var templates = new List<string>();
        for (var i = 0; i < text.Length - 2; i++)
        {
            if (text[i] != '{' || text[i + 1] != '{') continue;
            var start = i;
            var depth = 0;
            for (; i < text.Length - 1; i++)
            {
                if (text[i] == '{' && text[i + 1] == '{') { depth++; i++; continue; }
                if (text[i] == '}' && text[i + 1] == '}')
                {
                    depth--;
                    i++;
                    if (depth == 0)
                    {
                        var template = text[start..(i + 1)];
                        var name = template.Trim('{', '}').Split('|', 2)[0].Trim();
                        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            templates.Add(template);
                        break;
                    }
                }
            }
        }
        return templates;
    }

    private static Dictionary<string, string> ParseTemplateParameters(string template)
    {
        var inner = template.Trim();
        if (inner.StartsWith("{{")) inner = inner[2..];
        if (inner.EndsWith("}}")) inner = inner[..^2];
        var parts = SplitTopLevel(inner, '|');
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = 1;
        foreach (var part in parts.Skip(1))
        {
            var eq = IndexOfTopLevel(part, '=');
            if (eq > 0)
                result[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            else
                result[(positional++).ToString(CultureInfo.InvariantCulture)] = part.Trim();
        }
        return result;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        var braceDepth = 0;
        var linkDepth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (i < text.Length - 1 && text[i] == '{' && text[i + 1] == '{') { braceDepth++; sb.Append("{{"); i++; continue; }
            if (i < text.Length - 1 && text[i] == '}' && text[i + 1] == '}') { braceDepth = Math.Max(0, braceDepth - 1); sb.Append("}}"); i++; continue; }
            if (i < text.Length - 1 && text[i] == '[' && text[i + 1] == '[') { linkDepth++; sb.Append("[["); i++; continue; }
            if (i < text.Length - 1 && text[i] == ']' && text[i + 1] == ']') { linkDepth = Math.Max(0, linkDepth - 1); sb.Append("]]"); i++; continue; }
            if (text[i] == separator && braceDepth == 0 && linkDepth == 0)
            {
                parts.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(text[i]);
        }
        parts.Add(sb.ToString());
        return parts;
    }

    private static int IndexOfTopLevel(string text, char target)
    {
        var braceDepth = 0;
        var linkDepth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (i < text.Length - 1 && text[i] == '{' && text[i + 1] == '{') { braceDepth++; i++; continue; }
            if (i < text.Length - 1 && text[i] == '}' && text[i + 1] == '}') { braceDepth = Math.Max(0, braceDepth - 1); i++; continue; }
            if (i < text.Length - 1 && text[i] == '[' && text[i + 1] == '[') { linkDepth++; i++; continue; }
            if (i < text.Length - 1 && text[i] == ']' && text[i + 1] == ']') { linkDepth = Math.Max(0, linkDepth - 1); i++; continue; }
            if (text[i] == target && braceDepth == 0 && linkDepth == 0) return i;
        }
        return -1;
    }

    private static string CleanWikiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("<br>", " ").Replace("<br />", " ").Replace("<br/>", " ");
        text = Regex.Replace(text, @"<ref\b[^>/]*/>|<ref\b[^>]*>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\{\{ScPkmn\}\}", "Pokémon", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\{\{dotw\|([^}]+)\}\}", "($1)", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\{\{(?:p|m|t|a|type|pkmn|ga|rt|wp|game|g|DL|OBP|tt)\|([^{}|]+)(?:\|([^{}]+))?\}\}", m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\{\{[^{}|]+\|([^{}]+)\}\}", "$1");
        text = Regex.Replace(text, @"\{\{[^{}]+\}\}", "");
        text = Regex.Replace(text, @"\[\[([^|\]]+)\|([^\]]+)\]\]", "$2");
        text = Regex.Replace(text, @"\[\[([^\]]+)\]\]", "$1");
        text = text.Replace("'''", "").Replace("''", "").Replace("<sc>", "").Replace("</sc>", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string CleanHtmlText(string text)
    {
        return Regex.Replace(WebUtility.HtmlDecode(text ?? ""), @"\s+", " ").Trim();
    }

    private static string NormalizeGameSlug(string game)
    {
        var cleaned = CleanWikiText(game).Trim();
        if (GameInfo.TryGetValue(cleaned, out var info)) return info.Slug;
        return Regex.Replace(cleaned.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    }

    private static IEnumerable<T> Deduplicate<T>(IEnumerable<T> items, Func<T, string> keySelector)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (seen.Add(keySelector(item))) yield return item;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '-');
        return fileName;
    }

    private static (int Generation, string Slug, string Label, int Sort)? InferSpriteLabel(string fileName, int currentGeneration)
    {
        var lower = fileName.ToLowerInvariant();
        var candidates = new (string Token, string Label, int Gen, int Sort)[]
        {
            ("1g", "Red / Green", 1, 10), ("1b", "Red / Blue", 1, 12), ("1y", "Yellow", 1, 13),
            ("2g", "Gold", 2, 20), ("2s", "Silver", 2, 21), ("2c", "Crystal", 2, 22),
            ("3r", "Ruby / Sapphire", 3, 30), ("3e", "Emerald", 3, 32), ("3f", "FireRed / LeafGreen", 3, 33),
            ("4d", "Diamond / Pearl", 4, 40), ("4p", "Platinum", 4, 42), ("4h", "HeartGold / SoulSilver", 4, 43),
            ("5b", "Black / White", 5, 50), ("5b2", "Black 2 / White 2", 5, 52),
            ("6x", "X / Y", 6, 60), ("6o", "Omega Ruby / Alpha Sapphire", 6, 62),
            ("7s", "Sun / Moon", 7, 70), ("7u", "Ultra Sun / Ultra Moon", 7, 72), ("PE", "Let's Go Pikachu / Eevee", 7, 74),
            ("8b", "Brilliant Diamond / Shining Pearl", 8, 82), ("HOME", "HOME", 8, 86),
            ("9s", "Scarlet / Violet", 9, 90)
        };

        foreach (var c in candidates)
        {
            if (lower.Contains(c.Token.ToLowerInvariant()))
            {
                return (c.Gen, Regex.Replace(c.Label.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-'), c.Label, c.Sort);
            }
        }

        if (currentGeneration > 0)
            return (currentGeneration, $"generation-{currentGeneration}", $"Generation {currentGeneration}", currentGeneration * 10);
        return null;
    }
}
