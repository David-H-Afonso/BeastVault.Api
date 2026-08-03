using System.Globalization;
using System.Text.Json;
using BeastVault.Api.Application.Interfaces;

namespace BeastVault.Api.Infrastructure.Services;

public sealed class TcgDexProvider(IHttpClientFactory httpClientFactory) : ITcgDexProvider
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("TcgDex");

    public async Task<IReadOnlyList<TcgProviderSet>> GetSetsAsync(
        string language,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"v2/{NormalizeLanguage(language)}/sets", cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        return document.RootElement.EnumerateArray().Select(ParseSet).ToList();
    }

    public async Task<TcgProviderSet?> GetSetAsync(
        string setId,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await GetJsonAsync(
                $"v2/{NormalizeLanguage(language)}/sets/{Uri.EscapeDataString(setId)}",
                cancellationToken);
            return ParseSet(document.RootElement);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TcgProviderSet?> GetSetByOfficialCodeAsync(
        string officialCode,
        string language,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(
            $"v2/{NormalizeLanguage(language)}/sets?abbreviation.official={Uri.EscapeDataString(officialCode.Trim())}",
            cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var result in document.RootElement.EnumerateArray())
        {
            var setId = GetString(result, "id");
            if (string.IsNullOrWhiteSpace(setId)) continue;
            var set = await GetSetAsync(setId, language, cancellationToken);
            if (set is not null && string.Equals(set.OfficialCode, officialCode.Trim(), StringComparison.OrdinalIgnoreCase))
                return set;
        }
        return null;
    }

    public async Task<TcgProviderCard?> GetSetCardAsync(
        string setId,
        string localId,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await GetJsonAsync(
                $"v2/{NormalizeLanguage(language)}/sets/{Uri.EscapeDataString(setId)}/{Uri.EscapeDataString(localId)}",
                cancellationToken);
            return ParseCard(document.RootElement, isComplete: true);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TcgProviderCard?> GetCardAsync(
        string cardId,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await GetJsonAsync(
                $"v2/{NormalizeLanguage(language)}/cards/{Uri.EscapeDataString(cardId)}",
                cancellationToken);
            return ParseCard(document.RootElement, isComplete: true);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TcgProviderCard>> SearchCardsAsync(
        string? query,
        string? setId,
        string? number,
        int? speciesId,
        int page,
        int pageSize,
        string language,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
            parameters.Add($"name={Uri.EscapeDataString(query.Trim())}");
        if (!string.IsNullOrWhiteSpace(setId))
            parameters.Add($"set.id={Uri.EscapeDataString(setId.Trim())}");
        if (!string.IsNullOrWhiteSpace(number))
            parameters.Add($"localId={Uri.EscapeDataString(number.Trim())}");
        if (speciesId.HasValue)
            parameters.Add($"dexId={speciesId.Value}");
        parameters.Add($"pagination:page={Math.Clamp(page, 1, 10_000)}");
        parameters.Add($"pagination:itemsPerPage={Math.Clamp(pageSize, 1, 100)}");

        using var document = await GetJsonAsync(
            $"v2/{NormalizeLanguage(language)}/cards?{string.Join('&', parameters)}",
            cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        return document.RootElement.EnumerateArray().Select(value => ParseCard(value, isComplete: false)).ToList();
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static TcgProviderSet ParseSet(JsonElement value)
    {
        var cardCount = value.TryGetProperty("cardCount", out var counts) ? counts : default;
        var cards = value.TryGetProperty("cards", out var cardValues) && cardValues.ValueKind == JsonValueKind.Array
            ? cardValues.EnumerateArray().Select(value => ParseCard(value, isComplete: false)).ToList()
            : [];
        var id = GetString(value, "id") ?? string.Empty;
        var name = GetString(value, "name") ?? id;
        var series = value.TryGetProperty("serie", out var seriesValue) ? seriesValue : default;
        var abbreviation = value.TryGetProperty("abbreviation", out var abbreviationValue) ? abbreviationValue : default;
        return new TcgProviderSet(
            id,
            name,
            GetString(series, "name"),
            GetString(series, "id"),
            GetString(abbreviation, "official"),
            GetInt(cardCount, "official"),
            GetInt(cardCount, "total"),
            ParseDate(GetString(value, "releaseDate")),
            BuildAssetUrl(GetString(value, "symbol"), "symbol"),
            BuildAssetUrl(GetString(value, "logo"), "logo"),
            cards.Select(card => card with { SetId = id, SetName = name }).ToList());
    }

    private static TcgProviderCard ParseCard(JsonElement value, bool isComplete)
    {
        var id = GetString(value, "id") ?? string.Empty;
        var set = value.TryGetProperty("set", out var setValue) ? setValue : default;
        var setId = GetString(set, "id") ?? ExtractSetId(id);
        var setName = GetString(set, "name") ?? setId;
        var image = GetString(value, "image");
        var variants = ParseVariants(value);
        var dexIds = GetIntArray(value, "dexId");
        var (eur, eurVariants, cardmarketUrl, cardmarketUpdated) = ParseCardmarket(value);
        var (usd, usdVariants, tcgplayerUrl, tcgplayerUpdated) = ParseTcgplayer(value);
        var (detailedEur, detailedUsd) = ParseDetailedVariantPrices(value);
        foreach (var price in detailedEur) eurVariants[price.Key] = price.Value;
        foreach (var price in detailedUsd) usdVariants[price.Key] = price.Value;
        eur ??= detailedEur.Values.Cast<decimal?>().FirstOrDefault();
        usd ??= detailedUsd.Values.Cast<decimal?>().FirstOrDefault();

        return new TcgProviderCard(
            id,
            setId,
            setName,
            GetString(value, "name") ?? id,
            GetString(value, "localId") ?? GetString(value, "number") ?? string.Empty,
            GetString(value, "rarity"),
            GetString(value, "illustrator"),
            BuildCardImage(image, "low.webp"),
            BuildCardImage(image, "high.webp"),
            dexIds,
            variants.Count > 0 ? variants : ["normal"],
            eur,
            usd,
            eurVariants,
            usdVariants,
            Latest(cardmarketUpdated, tcgplayerUpdated),
            cardmarketUrl,
            tcgplayerUrl,
            isComplete,
            value.GetRawText());
    }

    private static List<string> ParseVariants(JsonElement value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value.TryGetProperty("variants_detailed", out var detailed) && detailed.ValueKind == JsonValueKind.Array)
        {
            foreach (var variant in detailed.EnumerateArray())
            {
                if (variant.ValueKind != JsonValueKind.Object) continue;
                var name = BuildDetailedVariantName(variant);
                if (name is not null) result.Add(name);
            }
        }

        if (value.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Object)
        {
            var firstEdition = variants.TryGetProperty("firstEdition", out var first) && first.ValueKind == JsonValueKind.True;
            var holo = variants.TryGetProperty("holo", out var holoValue) && holoValue.ValueKind == JsonValueKind.True;
            var normal = variants.TryGetProperty("normal", out var normalValue) && normalValue.ValueKind == JsonValueKind.True;
            foreach (var property in variants.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.True && !property.NameEquals("firstEdition"))
                    result.Add(NormalizeVariant(property.Name));
            }
            if (firstEdition && holo) result.Add("1st-edition-holo");
            if (firstEdition && normal) result.Add("1st-edition-normal");
            if (firstEdition && !holo && !normal) result.Add("1st-edition");
        }
        return result.OrderBy(x => x).ToList();
    }

    private static (decimal? Price, Dictionary<string, decimal> Variants, string? Url, DateTime? Updated) ParseCardmarket(JsonElement value)
    {
        if (!TryPricing(value, "cardmarket", out var market)) return (null, [], null, null);
        var variants = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var basePrice = FirstPositive(market, "trend", "avg", "avg7", "low");
        var reverse = FirstPositive(market, "trend-holo", "avg-holo", "low-holo");
        if (basePrice.HasValue) variants["normal"] = basePrice.Value;
        if (reverse.HasValue) variants["reverse"] = reverse.Value;
        return (
            basePrice ?? reverse,
            variants,
            GetString(market, "url"),
            ParseDate(GetString(market, "updated")));
    }

    private static (decimal? Price, Dictionary<string, decimal> Variants, string? Url, DateTime? Updated) ParseTcgplayer(JsonElement value)
    {
        if (!TryPricing(value, "tcgplayer", out var market)) return (null, [], null, null);
        var variants = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in market.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object || property.NameEquals("updated")) continue;
            var price = FirstPositive(property.Value, "marketPrice", "midPrice", "lowPrice");
            if (price.HasValue) variants[NormalizeVariant(property.Name)] = price.Value;
        }
        return (
            variants.Values.Cast<decimal?>().FirstOrDefault(),
            variants,
            GetString(market, "url"),
            ParseDate(GetString(market, "updated")));
    }

    private static (Dictionary<string, decimal> Eur, Dictionary<string, decimal> Usd) ParseDetailedVariantPrices(JsonElement value)
    {
        var eur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var usd = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!value.TryGetProperty("variants_detailed", out var variants) || variants.ValueKind != JsonValueKind.Array)
            return (eur, usd);

        foreach (var variant in variants.EnumerateArray())
        {
            if (variant.ValueKind != JsonValueKind.Object) continue;
            var name = BuildDetailedVariantName(variant);
            if (name is null || !variant.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object)
                continue;

            if (pricing.TryGetProperty("cardmarket", out var cardmarket) && cardmarket.ValueKind == JsonValueKind.Object)
            {
                var price = FirstPositive(cardmarket, "trend", "avg", "avg7", "low");
                if (price.HasValue) eur[name] = price.Value;
            }

            if (pricing.TryGetProperty("tcgplayer", out var tcgplayer) && tcgplayer.ValueKind == JsonValueKind.Object)
            {
                foreach (var bucket in tcgplayer.EnumerateObject())
                {
                    if (bucket.Value.ValueKind != JsonValueKind.Object) continue;
                    var price = FirstPositive(bucket.Value, "marketPrice", "midPrice", "lowPrice");
                    if (price.HasValue)
                    {
                        usd[name] = price.Value;
                        break;
                    }
                }
            }
        }
        return (eur, usd);
    }

    private static string? BuildDetailedVariantName(JsonElement variant)
    {
        var parts = new List<string>();
        AddPart(parts, GetString(variant, "stamp"));
        if (variant.TryGetProperty("stamp", out var stamps) && stamps.ValueKind == JsonValueKind.Array)
            parts.AddRange(stamps.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))!);
        AddPart(parts, GetString(variant, "subtype"));
        AddPart(parts, GetString(variant, "type"));
        AddPart(parts, GetString(variant, "foil"));
        var size = GetString(variant, "size");
        if (!string.Equals(size, "standard", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(size, "estándar", StringComparison.OrdinalIgnoreCase))
        {
            AddPart(parts, size);
        }
        return parts.Count == 0 ? null : NormalizeVariant(string.Join('-', parts));
    }

    private static bool TryPricing(JsonElement value, string provider, out JsonElement result)
    {
        result = default;
        return value.TryGetProperty("pricing", out var pricing) &&
            pricing.ValueKind == JsonValueKind.Object &&
            pricing.TryGetProperty(provider, out result) &&
            result.ValueKind == JsonValueKind.Object;
    }

    internal static string NormalizeVariant(string value)
    {
        var normalized = new string(value.Trim()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Replace('_', '-');
        var result = new System.Text.StringBuilder();
        foreach (var character in normalized)
        {
            if (char.IsUpper(character) && result.Length > 0 && result[^1] != '-') result.Append('-');
            result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString()
            .Replace("holofoil", "holo")
            .Replace("reverse-holo", "reverse")
            .Replace("first-edition", "1st-edition")
            .Replace("basico", "normal")
            .Replace("reversa", "reverse")
            .Replace("holographic", "holo")
            .Replace("unlimited-holo", "unlimited-holo");
    }

    private static void AddPart(ICollection<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) parts.Add(value);
    }

    private static string NormalizeLanguage(string language) => language.Equals("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
    private static string ExtractSetId(string cardId) => cardId.Contains('-') ? cardId[..cardId.LastIndexOf('-')] : string.Empty;
    private static string? BuildCardImage(string? value, string suffix)
    {
        var safe = AllowHttpsUrl(value, "assets.tcgdex.net");
        return safe is null ? null : $"{safe.TrimEnd('/')}/{suffix}";
    }

    private static string? BuildAssetUrl(string? value, string _)
    {
        var safe = AllowHttpsUrl(value, "assets.tcgdex.net");
        if (safe is null || !Uri.TryCreate(safe, UriKind.Absolute, out var uri)) return null;
        if (Path.HasExtension(uri.AbsolutePath)) return uri.ToString();
        var builder = new UriBuilder(uri) { Path = $"{uri.AbsolutePath.TrimEnd('/')}.webp" };
        return builder.Uri.ToString();
    }

    private static string? AllowHttpsUrl(string? value, params string[] hosts)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        return hosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)) ? uri.ToString() : null;
    }
    private static DateTime? Latest(DateTime? first, DateTime? second)
    {
        if (!first.HasValue) return second;
        if (!second.HasValue) return first;
        return first.Value >= second.Value ? first : second;
    }

    private static string? GetString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString()
            : null;

    private static int GetInt(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) &&
            item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var result)
            ? result
            : 0;

    private static List<int> GetIntArray(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.Array
            ? item.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
                .Select(x => x.GetInt32()).Distinct().ToList()
            : [];

    private static decimal? FirstPositive(JsonElement value, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) &&
                item.ValueKind == JsonValueKind.Number && item.TryGetDecimal(out var result) && result > 0)
                return result;
        }
        return null;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
}
