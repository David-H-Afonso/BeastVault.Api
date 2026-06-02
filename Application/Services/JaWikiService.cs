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

public interface IJaWikiService
{
    Task<int> FetchJaFlavorEntriesAsync(int speciesId);
}

/// <summary>
/// Fetches Japanese Pokédex flavor text from the Japanese Pokémon wiki (wiki.pokemonwiki.com)
/// which is Bulbapedia's official Japanese partner wiki and covers all generations.
///
/// Note: The wiki is behind Cloudflare bot protection. This service detects Cloudflare
/// challenge responses and returns 0 entries gracefully. From environments not flagged
/// by Cloudflare (e.g. home servers / CasaOS), the fetch should succeed.
/// </summary>
public class JaWikiService : IJaWikiService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JaWikiService> _logger;

    private const string JaWikiBase = "https://wiki.pokemonwiki.com/w/api.php";
    private const string BulbapediaBase = "https://bulbapedia.bulbagarden.net/w/api.php";

    /// <summary>
    /// Maps template parameter names used in the Japanese wiki to one or more game slugs.
    /// wiki.pokemonwiki.com uses abbreviated Japanese game names as template parameters.
    /// </summary>
    private static readonly Dictionary<string, string[]> ParamToSlugs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Gen 1
            ["赤"] = ["red"],
            ["緑"] = ["green"],
            ["青"] = ["blue"],
            ["赤青"] = ["red-blue"],
            ["赤緑"] = ["red-blue"],
            ["RB"] = ["red-blue"],
            ["ピカチュウ"] = ["yellow"],
            ["Y"] = ["yellow"],
            ["スタジアム"] = ["stadium"],
            // Gen 2
            ["金"] = ["gold"],
            ["銀"] = ["silver"],
            ["クリスタル"] = ["crystal"],
            ["スタジアム2"] = ["stadium-2"],
            // Gen 3
            ["ルビー"] = ["ruby"],
            ["サファイア"] = ["sapphire"],
            ["エメラルド"] = ["emerald"],
            ["ファイアレッド"] = ["firered"],
            ["リーフグリーン"] = ["leafgreen"],
            ["FR"] = ["firered"],
            ["LG"] = ["leafgreen"],
            ["FRLG"] = ["firered", "leafgreen"],
            // Gen 4
            ["ダイヤモンド"] = ["diamond"],
            ["パール"] = ["pearl"],
            ["プラチナ"] = ["platinum"],
            ["ハートゴールド"] = ["heartgold"],
            ["ソウルシルバー"] = ["soulsilver"],
            ["HG"] = ["heartgold"],
            ["SS"] = ["soulsilver"],
            ["HGSS"] = ["heartgold", "soulsilver"],
            // Gen 5
            ["ブラック"] = ["black"],
            ["ホワイト"] = ["white"],
            ["ブラック2"] = ["black-2"],
            ["ホワイト2"] = ["white-2"],
            ["BW"] = ["black", "white"],
            ["BW2"] = ["black-2", "white-2"],
            // Gen 6
            ["X"] = ["x"],
            ["Y"] = ["y"],
            ["XY"] = ["x", "y"],
            ["オメガルビー"] = ["omega-ruby"],
            ["アルファサファイア"] = ["alpha-sapphire"],
            ["OR"] = ["omega-ruby"],
            ["AS"] = ["alpha-sapphire"],
            ["ORAS"] = ["omega-ruby", "alpha-sapphire"],
            // Gen 7
            ["サン"] = ["sun"],
            ["ムーン"] = ["moon"],
            ["SM"] = ["sun", "moon"],
            ["ウルトラサン"] = ["ultra-sun"],
            ["ウルトラムーン"] = ["ultra-moon"],
            ["USUM"] = ["ultra-sun", "ultra-moon"],
            ["US"] = ["ultra-sun"],
            ["UM"] = ["ultra-moon"],
            ["LGP"] = ["lets-go-pikachu"],
            ["LGE"] = ["lets-go-eevee"],
            ["LGPE"] = ["lets-go-pikachu", "lets-go-eevee"],
            ["ピカブイ"] = ["lets-go-pikachu", "lets-go-eevee"],
            // Gen 8
            ["ソード"] = ["sword"],
            ["シールド"] = ["shield"],
            ["剣"] = ["sword"],
            ["盾"] = ["shield"],
            ["剣盾"] = ["sword", "shield"],
            ["SW"] = ["sword"],
            ["SH"] = ["shield"],
            ["SWSH"] = ["sword", "shield"],
            ["BD"] = ["brilliant-diamond"],
            ["SP"] = ["shining-pearl"],
            ["BDSP"] = ["brilliant-diamond", "shining-pearl"],
            ["ブリリアントダイヤモンド"] = ["brilliant-diamond"],
            ["シャイニングパール"] = ["shining-pearl"],
            ["LA"] = ["legends-arceus"],
            ["レジェンズアルセウス"] = ["legends-arceus"],
            // Gen 9
            ["スカーレット"] = ["scarlet"],
            ["バイオレット"] = ["violet"],
            ["SV"] = ["scarlet", "violet"],
            ["ZA"] = ["legends-za"],
            ["レジェンズZA"] = ["legends-za"],
            ["ポコピア"] = ["pokopia"],
        };

    public JaWikiService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<JaWikiService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> FetchJaFlavorEntriesAsync(int speciesId)
    {
        var species = await _db.PokedexEntries.FindAsync(speciesId);
        if (species == null) return 0;

        var jaPageName = await GetJaPageNameAsync(speciesId, species.Name);
        if (string.IsNullOrEmpty(jaPageName)) return 0;

        var client = _httpClientFactory.CreateClient("JaWiki");
        var url = $"{JaWikiBase}?action=parse&format=json&page={Uri.EscapeDataString(jaPageName)}&prop=wikitext";

        string wikitext;
        try
        {
            var response = await client.GetAsync(url);

            // Cloudflare often responds 403 for bot traffic
            if ((int)response.StatusCode == 403 || (int)response.StatusCode == 503)
            {
                _logger.LogDebug("JaWiki returned HTTP {StatusCode} (Cloudflare?) for species {SpeciesId}",
                    (int)response.StatusCode, speciesId);
                return 0;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("JaWiki HTTP {StatusCode} for species {SpeciesId} (page: {Page})",
                    (int)response.StatusCode, speciesId, jaPageName);
                return 0;
            }

            var raw = await response.Content.ReadAsStringAsync();

            // Detect Cloudflare challenge page (served with 200 but contains JS challenge)
            if (IsCloudflareChallenge(raw))
            {
                _logger.LogDebug("JaWiki Cloudflare challenge detected for species {SpeciesId}", speciesId);
                return 0;
            }

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out _)) return 0;

            var parse = doc.RootElement.GetProperty("parse");
            wikitext = parse.TryGetProperty("wikitext", out var wt) && wt.TryGetProperty("*", out var wtStar)
                ? wtStar.GetString() ?? ""
                : "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JaWiki fetch failed for species {SpeciesId}", speciesId);
            return 0;
        }

        if (string.IsNullOrWhiteSpace(wikitext)) return 0;

        var entries = ParseJaWikiFlavorEntries(wikitext);
        if (entries.Count == 0) return 0;

        await _db.PokedexFlavorEntries
            .Where(f => f.SpeciesId == speciesId && f.Source == CacheSource.JaWiki)
            .ExecuteDeleteAsync();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var (slug, text) in entries)
        {
            if (!seen.Add($"{slug}|{text}")) continue;
            _db.PokedexFlavorEntries.Add(new PokedexFlavorEntry
            {
                SpeciesId = speciesId,
                Language = "ja",
                GameVersion = slug,
                Text = text,
                Source = CacheSource.JaWiki,
                CachedAt = DateTime.UtcNow
            });
            count++;
        }

        if (count > 0)
            await _db.SaveChangesAsync();

        _logger.LogDebug("JaWiki saved {Count} ja flavor entries for species {SpeciesId}", count, speciesId);
        return count;
    }

    // Get the Japanese wiki page title via Bulbapedia's interlanguage link API.
    private async Task<string?> GetJaPageNameAsync(int speciesId, string speciesName)
    {
        var cache = await _db.BulbapediaCache
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpeciesId == speciesId);

        var pageTitle = cache?.PageTitle ?? BuildBulbapediaPageName(speciesName);

        var client = _httpClientFactory.CreateClient("Bulbapedia");
        var url = $"{BulbapediaBase}?action=query&format=json&titles={Uri.EscapeDataString(pageTitle)}&prop=langlinks&lllang=ja&lllimit=1";

        try
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
            foreach (var page in pages.EnumerateObject())
            {
                if (!page.Value.TryGetProperty("langlinks", out var langlinks)) continue;
                foreach (var link in langlinks.EnumerateArray())
                {
                    if (link.TryGetProperty("*", out var title))
                        return title.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulbapedia langlinks lookup failed for species {SpeciesId}", speciesId);
        }

        return null;
    }

    // Parse all flavor entries from the Japanese wiki page wikitext.
    // Tries multiple common template names used on Japanese Pokémon wikis.
    private static List<(string Slug, string Text)> ParseJaWikiFlavorEntries(string wikitext)
    {
        // Japanese Pokémon wikis (Bulbapedia partner) typically use one of these template names
        var candidateTemplates = new[]
        {
            "ポケモン図鑑",
            "Pokédex",
            "Pokedex",
            "図鑑",
            "ずかん",
            "ポケモン説明",
        };

        Dictionary<string, string>? rawParams = null;
        foreach (var name in candidateTemplates)
        {
            var start = FindTemplateStart(wikitext, name);
            if (start < 0) continue;
            var content = ExtractTemplateContent(wikitext, start);
            if (!string.IsNullOrEmpty(content))
            {
                rawParams = ParseKeyValuePairs(content);
                break;
            }
        }

        if (rawParams == null || rawParams.Count == 0) return [];

        var results = new List<(string Slug, string Text)>();
        foreach (var paramName in rawParams.Keys)
        {
            if (!ParamToSlugs.TryGetValue(paramName, out var slugs)) continue;

            var resolvedText = ResolveValue(rawParams, paramName);
            if (string.IsNullOrWhiteSpace(resolvedText)) continue;

            resolvedText = CleanWikiText(resolvedText);
            if (!IsValidFlavorText(resolvedText)) continue;

            foreach (var slug in slugs)
                results.Add((slug, PokedexTextFilters.CleanFlavorText(resolvedText)));
        }

        return results;
    }

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

    private static Dictionary<string, string> ParseKeyValuePairs(string templateContent)
    {
        var inner = templateContent;
        if (inner.StartsWith("{{")) inner = inner[2..];
        if (inner.EndsWith("}}")) inner = inner[..^2];

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = SplitTopLevel(inner, '|');

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

    private static string ResolveValue(Dictionary<string, string> rawParams, string key, int depth = 0)
    {
        if (depth > 10 || !rawParams.TryGetValue(key, out var value)) return "";
        var trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains('\n') && rawParams.ContainsKey(trimmed))
            return ResolveValue(rawParams, trimmed, depth + 1);
        return trimmed;
    }

    private static string CleanWikiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("<br>", " ").Replace("<br />", " ").Replace("<br/>", " ");
        text = Regex.Replace(text, @"<ref\b[^>/]*/>|<ref\b[^>]*>.*?</ref>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\[\[(?:[^\]|]+\|)?([^\]]+)\]\]", "$1");
        text = Regex.Replace(text, @"\{\{[^{}]*\}\}", "");
        text = text.Replace("'''", "").Replace("''", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static bool IsValidFlavorText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 5) return false;
        return true;
    }

    private static bool IsCloudflareChallenge(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        var trimmed = content.TrimStart();
        // JSON responses always start with { or [; anything else is HTML/Cloudflare
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('[')) return true;
        return false;
    }

    private static string BuildBulbapediaPageName(string speciesName)
    {
        var name = CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(speciesName.Replace("-", " ").ToLowerInvariant())
            .Replace(" ", "_");
        return $"{name}_(Pokémon)";
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
}
