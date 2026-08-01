using System.Globalization;
using System.Text.Json;
using BeastVault.Api.Application.Interfaces;

namespace BeastVault.Api.Infrastructure.Services;

public sealed class PokemonTcgIoProvider(IHttpClientFactory httpClientFactory) : IPokemonTcgIoProvider
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("PokemonTcgIo");

    public async Task<TcgProviderCard?> GetCardAsync(
        string cardId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v2/cards/{Uri.EscapeDataString(cardId)}");
        request.Headers.Add("X-Api-Key", apiKey);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var value)) return null;

        var set = value.TryGetProperty("set", out var setValue) ? setValue : default;
        var cardmarket = value.TryGetProperty("cardmarket", out var cm) ? cm : default;
        var tcgplayer = value.TryGetProperty("tcgplayer", out var tp) ? tp : default;
        var variantsEur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var variantsUsd = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal? eur = null;
        decimal? usd = null;

        if (cardmarket.ValueKind == JsonValueKind.Object && cardmarket.TryGetProperty("prices", out var cmPrices))
            eur = FirstPositive(cmPrices, "trendPrice", "averageSellPrice", "avg7", "lowPrice");
        if (eur.HasValue) variantsEur["normal"] = eur.Value;

        if (tcgplayer.ValueKind == JsonValueKind.Object && tcgplayer.TryGetProperty("prices", out var tpPrices))
        {
            foreach (var property in tpPrices.EnumerateObject())
            {
                var price = FirstPositive(property.Value, "market", "mid", "low");
                if (price.HasValue) variantsUsd[TcgDexProvider.NormalizeVariant(property.Name)] = price.Value;
            }
            usd = variantsUsd.Values.Cast<decimal?>().FirstOrDefault();
        }

        var image = value.TryGetProperty("images", out var images) ? images : default;
        return new TcgProviderCard(
            GetString(value, "id") ?? cardId,
            GetString(set, "id") ?? string.Empty,
            GetString(set, "name") ?? string.Empty,
            GetString(value, "name") ?? cardId,
            GetString(value, "number") ?? string.Empty,
            GetString(value, "rarity"),
            GetString(value, "artist"),
            AllowHttpsUrl(GetString(image, "small"), "images.pokemontcg.io"),
            AllowHttpsUrl(GetString(image, "large"), "images.pokemontcg.io"),
            GetIntArray(value, "nationalPokedexNumbers"),
            variantsUsd.Keys.Concat(variantsEur.Keys).Distinct().DefaultIfEmpty("normal").ToList(),
            eur,
            usd,
            variantsEur,
            variantsUsd,
            Latest(ParseDate(GetString(cardmarket, "updatedAt")), ParseDate(GetString(tcgplayer, "updatedAt"))),
            AllowHttpsUrl(GetString(cardmarket, "url"), "prices.pokemontcg.io", "cardmarket.com"),
            AllowHttpsUrl(GetString(tcgplayer, "url"), "prices.pokemontcg.io", "tcgplayer.com"));
    }

    private static string? GetString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;

    private static List<int> GetIntArray(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.Array
            ? item.EnumerateArray().Where(x => x.TryGetInt32(out _)).Select(x => x.GetInt32()).Distinct().ToList()
            : [];

    private static decimal? FirstPositive(JsonElement value, params string[] properties)
    {
        foreach (var property in properties)
            if (value.TryGetProperty(property, out var item) && item.TryGetDecimal(out var result) && result > 0) return result;
        return null;
    }

    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;
    private static DateTime? Latest(DateTime? first, DateTime? second) => first > second ? first : second;

    private static string? AllowHttpsUrl(string? value, params string[] hosts)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return null;
        return hosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)) ? uri.ToString() : null;
    }
}
