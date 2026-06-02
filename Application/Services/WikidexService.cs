using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public interface IWikidexService
{
    Task<int> FetchEsFlavorEntriesAsync(int speciesId);
}

public class WikidexService : IWikidexService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WikidexService> _logger;
    private const string BaseUrl = "https://www.wikidex.net/api.php";

    /// <summary>
    /// Maps wikidex {{Pokédex}} parameter names (Spanish game names) to one or more game slugs.
    /// When lgpe appears, creates entries for both Let's Go games since they share flavor text.
    /// </summary>
    private static readonly Dictionary<string, string[]> ParamToSlugs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rojoyazul"] = ["red-blue"],
            ["rojo"] = ["red"],
            ["azul"] = ["blue"],
            ["amarillo"] = ["yellow"],
            ["oro"] = ["gold"],
            ["plata"] = ["silver"],
            ["cristal"] = ["crystal"],
            ["rubí"] = ["ruby"],
            ["rubi"] = ["ruby"],
            ["zafiro"] = ["sapphire"],
            ["esmeralda"] = ["emerald"],
            ["rojofuego"] = ["firered"],
            ["verdehoja"] = ["leafgreen"],
            ["diamante"] = ["diamond"],
            ["perla"] = ["pearl"],
            ["platino"] = ["platinum"],
            ["oro heartgold"] = ["heartgold"],
            ["plata soulsilver"] = ["soulsilver"],
            ["negro"] = ["black"],
            ["blanco"] = ["white"],
            ["negro 2"] = ["black-2"],
            ["blanco 2"] = ["white-2"],
            ["x"] = ["x"],
            ["y"] = ["y"],
            ["rubí omega"] = ["omega-ruby"],
            ["rubi omega"] = ["omega-ruby"],
            ["zafiro alfa"] = ["alpha-sapphire"],
            ["sol"] = ["sun"],
            ["luna"] = ["moon"],
            ["ultrasol"] = ["ultra-sun"],
            ["ultraluna"] = ["ultra-moon"],
            ["lgpe"] = ["lets-go-pikachu", "lets-go-eevee"],
            ["lgp"] = ["lets-go-pikachu"],
            ["lge"] = ["lets-go-eevee"],
            ["espada"] = ["sword"],
            ["escudo"] = ["shield"],
            ["diamante brillante"] = ["brilliant-diamond"],
            ["perla reluciente"] = ["shining-pearl"],
            ["leyendas arceus"] = ["legends-arceus"],
            ["escarlata"] = ["scarlet"],
            ["púrpura"] = ["violet"],
            ["purpura"] = ["violet"],
            ["leyendas za"] = ["legends-za"],
            ["leyendas ZA"] = ["legends-za"],
            ["pokopia"] = ["pokopia"],
            ["stadium"] = ["stadium"],
            ["stadium 2"] = ["stadium-2"],
        };

    public WikidexService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<WikidexService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> FetchEsFlavorEntriesAsync(int speciesId)
    {
        var species = await _db.PokedexEntries.FindAsync(speciesId);
        if (species == null) return 0;

        var pageName = await GetWikidexPageNameAsync(speciesId, species.Name);
        if (string.IsNullOrEmpty(pageName)) return 0;

        var client = _httpClientFactory.CreateClient("Wikidex");
        var url = $"{BaseUrl}?action=parse&format=json&page={Uri.EscapeDataString(pageName)}&prop=wikitext";

        string wikitext;
        try
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("WikiDex HTTP {StatusCode} for species {SpeciesId} (page: {PageName})",
                    (int)response.StatusCode, speciesId, pageName);
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out _)) return 0;

            var parse = doc.RootElement.GetProperty("parse");
            wikitext = parse.TryGetProperty("wikitext", out var wt) && wt.TryGetProperty("*", out var wtStar)
                ? wtStar.GetString() ?? ""
                : "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WikiDex fetch failed for species {SpeciesId}", speciesId);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(wikitext)) return 0;

        var entries = ParseWikidexFlavorEntries(wikitext);
        if (entries.Count == 0) return 0;

        await _db.PokedexFlavorEntries
            .Where(f => f.SpeciesId == speciesId && f.Source == CacheSource.WikiDex)
            .ExecuteDeleteAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var (slug, text) in entries)
        {
            if (!seen.Add($"{slug}|{text}")) continue;
            _db.PokedexFlavorEntries.Add(new PokedexFlavorEntry
            {
                SpeciesId = speciesId,
                Language = "es",
                GameVersion = slug,
                Text = text,
                Source = CacheSource.WikiDex,
                CachedAt = DateTime.UtcNow
            });
            count++;
        }

        if (count > 0)
            await _db.SaveChangesAsync();

        _logger.LogDebug("WikiDex saved {Count} es flavor entries for species {SpeciesId}", count, speciesId);
        return count;
    }

    // Derive the wikidex.net page name for a species.
    // Wikidex uses just the Pokémon name (no _(Pokémon) suffix).
    // If a BulbapediaCache record exists its PageTitle gives us the canonical name.
    private async Task<string?> GetWikidexPageNameAsync(int speciesId, string speciesName)
    {
        var cache = await _db.BulbapediaCache
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpeciesId == speciesId);

        if (cache != null && !string.IsNullOrEmpty(cache.PageTitle))
        {
            var title = cache.PageTitle;
            var idx = title.LastIndexOf("_(", StringComparison.OrdinalIgnoreCase);
            return idx > 0 ? title[..idx] : title;
        }

        // Fall back: title-case the English PokeAPI name (e.g. "bulbasaur" → "Bulbasaur")
        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(speciesName.Replace("-", " ").ToLowerInvariant())
            .Replace(" ", "_");
    }

    // Parse all flavor entries from a wikidex wikitext page.
    private static List<(string Slug, string Text)> ParseWikidexFlavorEntries(string wikitext)
    {
        var rawParams = ExtractPokédexParams(wikitext);
        if (rawParams.Count == 0) return [];

        var results = new List<(string Slug, string Text)>();

        foreach (var paramName in rawParams.Keys)
        {
            if (!ParamToSlugs.TryGetValue(paramName, out var slugs)) continue;

            var resolvedText = ResolveValue(rawParams, paramName);
            if (string.IsNullOrWhiteSpace(resolvedText)) continue;

            resolvedText = ExtractNombreHaEs(resolvedText);
            resolvedText = CleanWikiText(resolvedText);

            if (!IsValidFlavorText(resolvedText)) continue;

            foreach (var slug in slugs)
                results.Add((slug, PokedexTextFilters.CleanFlavorText(resolvedText)));
        }

        return results;
    }

    // Locate and parse all key=value parameters inside the {{Pokédex ...}} template.
    private static Dictionary<string, string> ExtractPokédexParams(string wikitext)
    {
        var templateStart = FindTemplateStart(wikitext, "Pokédex");
        if (templateStart < 0)
            templateStart = FindTemplateStart(wikitext, "Pokedex");
        if (templateStart < 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var content = ExtractTemplateContent(wikitext, templateStart);
        if (string.IsNullOrEmpty(content))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return ParseKeyValuePairs(content);
    }

    // Find the start index of a {{templateName ...}} block in text.
    private static int FindTemplateStart(string text, string templateName)
    {
        var i = 0;
        while (i < text.Length - 1)
        {
            if (text[i] != '{' || text[i + 1] != '{') { i++; continue; }

            var nameStart = i + 2;
            var nameEnd = nameStart;
            while (nameEnd < text.Length
                   && text[nameEnd] != '|' && text[nameEnd] != '\n'
                   && !(nameEnd + 1 < text.Length && text[nameEnd] == '}' && text[nameEnd + 1] == '}'))
                nameEnd++;

            var name = text[nameStart..nameEnd].Trim();
            if (name.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                return i;

            i++;
        }
        return -1;
    }

    // Extract the full template text (including {{ and }}) starting at position start.
    private static string ExtractTemplateContent(string text, int start)
    {
        var depth = 0;
        var i = start;
        while (i < text.Length - 1)
        {
            if (text[i] == '{' && text[i + 1] == '{') { depth++; i += 2; continue; }
            if (text[i] == '}' && text[i + 1] == '}')
            {
                depth--;
                i += 2;
                if (depth == 0) return text[start..i];
                continue;
            }
            i++;
        }
        return "";
    }

    // Build a key→rawValue dictionary from a template's content string.
    private static Dictionary<string, string> ParseKeyValuePairs(string templateContent)
    {
        var inner = templateContent;
        if (inner.StartsWith("{{")) inner = inner[2..];
        if (inner.EndsWith("}}")) inner = inner[..^2];

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = SplitTopLevel(inner, '|');

        // Skip first part (template name)
        foreach (var part in parts.Skip(1))
        {
            var eqIdx = IndexOfTopLevel(part, '=');
            if (eqIdx < 1) continue;
            var key = part[..eqIdx].Trim();
            var value = part[(eqIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }

        return result;
    }

    // Recursively resolve reference values (e.g. "zafiro = rubí" means use rubí's value).
    private static string ResolveValue(Dictionary<string, string> rawParams, string key, int depth = 0)
    {
        if (depth > 10 || !rawParams.TryGetValue(key, out var value)) return "";
        var trimmed = value.Trim();
        // A one-word value that exactly matches another key is a reference
        if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains('\n') && rawParams.ContainsKey(trimmed))
            return ResolveValue(rawParams, trimmed, depth + 1);
        return trimmed;
    }

    // Replace {{NombreHaEs|European text|LatinAm text}} with the European Spanish (first param).
    private static string ExtractNombreHaEs(string text)
    {
        return Regex.Replace(
            text,
            @"\{\{NombreHaEs\|(.*?)(?:\|.*?)?\}\}",
            m => m.Groups[1].Value.Trim(),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    private static string CleanWikiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("<br>", " ").Replace("<br />", " ").Replace("<br/>", " ");
        text = Regex.Replace(text, @"<ref\b[^>/]*/>|<ref\b[^>]*>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        // Wikilinks: [[target|display]] → display, [[target]] → target
        text = Regex.Replace(text, @"\[\[(?:[^\]|]+\|)?([^\]]+)\]\]", "$1");
        // Remove remaining templates
        text = Regex.Replace(text, @"\{\{[^{}]*\}\}", "");
        text = text.Replace("'''", "").Replace("''", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool IsValidFlavorText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 10) return false;
        if (text.Contains("No hay entrada", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Contains("no tiene entrada", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    // Split text by separator at top (non-nested) level, respecting {{ }} and [[ ]].
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

    // Find the first occurrence of target character at the top (non-nested) level.
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
}
