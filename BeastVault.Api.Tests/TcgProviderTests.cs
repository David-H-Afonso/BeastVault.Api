using System.Net;
using System.Reflection;
using System.Text;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure.Services;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class TcgProviderTests
{
    [Fact]
    public async Task TcgDex_ParsesLocalizedVariantsAndMarketplacePrices()
    {
        const string json = """
        {
          "id":"cel25-5","localId":"5","name":"Pikachu","image":"https://assets.tcgdex.net/es/swsh/cel25/5",
          "set":{"id":"cel25","name":"Celebraciones"},"dexId":[25],
          "variants":{"firstEdition":false,"holo":true,"normal":false,"reverse":false,"wPromo":false},
          "variants_detailed":[{"type":"holo","size":"estándar","pricing":{
            "cardmarket":{"trend":4.61,"updated":"2026-08-01T08:03:04Z"},
            "tcgplayer":{"updated":"2026-08-01T08:03:02Z","holofoil":{"marketPrice":5.9}}
          }}],
          "pricing":{"cardmarket":{"trend":4.61,"updated":"2026-08-01T08:03:04Z"},
                     "tcgplayer":{"updated":"2026-08-01T08:03:02Z","holofoil":{"marketPrice":5.9}}}
        }
        """;
        var provider = new TcgDexProvider(Factory(json));

        var card = await provider.GetCardAsync("cel25-5", "es", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("Pikachu", card.Name);
        Assert.Equal([25], card.NationalPokedexNumbers);
        Assert.Equal(["holo"], card.Variants);
        Assert.Equal(4.61m, card.PriceEur);
        Assert.Equal(5.9m, card.PriceUsd);
        Assert.Equal(4.61m, card.VariantPricesEur["holo"]);
        Assert.Equal(5.9m, card.VariantPricesUsd["holo"]);
        Assert.EndsWith("/high.webp", card.ImageLarge);
        Assert.True(card.IsComplete);
    }

    [Theory]
    [InlineData("svp-216", "216")]
    [InlineData("mep-011", "011")]
    public async Task TcgDex_PromoNullPricesAndDexValues_DoNotThrow(string cardId, string localId)
    {
        const string template = """
        {
          "id":"CARD_ID","localId":"LOCAL_ID","name":"Promo","image":null,
          "set":{"id":"SET_ID","name":"Promos"},
          "dexId":[null,"25"],
          "variants":{"normal":true},
          "pricing":{
            "cardmarket":{"trend":null,"avg":null,"avg7":null,"low":null,"updated":null},
            "tcgplayer":{"updated":null,"normal":{"marketPrice":null,"midPrice":null}}
          },
          "variants_detailed":[{"type":"normal","pricing":{"cardmarket":{"trend":null},"tcgplayer":null}}]
        }
        """;
        var json = template
            .Replace("CARD_ID", cardId, StringComparison.Ordinal)
            .Replace("LOCAL_ID", localId, StringComparison.Ordinal)
            .Replace("SET_ID", cardId.Split('-')[0], StringComparison.Ordinal);
        var provider = new TcgDexProvider(Factory(json));

        var card = await provider.GetCardAsync(cardId, "es", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal(localId, card.Number);
        Assert.Empty(card.NationalPokedexNumbers);
        Assert.Null(card.PriceEur);
        Assert.Null(card.PriceUsd);
        Assert.Null(card.PriceUpdatedAt);
    }

    [Fact]
    public async Task TcgDex_SetParsesOfficialMetadataAndAddsWebpAssetSuffixes()
    {
        const string json = """
        {
          "id":"svp","name":"Scarlet & Violet Promos",
          "serie":{"id":"sv","name":"Scarlet & Violet"},
          "abbreviation":{"official":"SVP"},
          "cardCount":{"official":null,"total":null},
          "symbol":"https://assets.tcgdex.net/en/sv/svp/symbol",
          "logo":"https://assets.tcgdex.net/en/sv/svp/logo"
        }
        """;
        var provider = new TcgDexProvider(Factory(json));

        var set = await provider.GetSetAsync("svp", "en", CancellationToken.None);

        Assert.NotNull(set);
        Assert.Equal("sv", set.SeriesId);
        Assert.Equal("SVP", set.OfficialCode);
        Assert.EndsWith("symbol.webp", set.SymbolUrl);
        Assert.EndsWith("logo.webp", set.LogoUrl);
        Assert.Equal(0, set.PrintedTotal);
    }

    [Fact]
    public async Task TcgDex_SearchUsesPlainFiltersAndMarksCardsBrief()
    {
        const string json = """[{"id":"svp-216","localId":"216","name":"Promo","set":{"id":"svp"}}]""";
        Uri? requested = null;
        var provider = new TcgDexProvider(Factory(json, request => requested = request.RequestUri));

        var cards = await provider.SearchCardsAsync(null, "svp", "216", 25, 1, 30, "en", CancellationToken.None);

        var query = requested?.Query ?? string.Empty;
        Assert.Contains("set.id=svp", query);
        Assert.Contains("localId=216", query);
        Assert.Contains("dexId=25", query);
        Assert.DoesNotContain("eq:", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eq%3A", query, StringComparison.OrdinalIgnoreCase);
        Assert.False(Assert.Single(cards).IsComplete);
    }

    [Fact]
    public async Task TcgDex_OfficialLookupAndExactCollectorUseExpectedRoutes()
    {
        var paths = new List<string>();
        var provider = new TcgDexProvider(Factory("{}", request => paths.Add(request.RequestUri!.PathAndQuery)));

        await provider.GetSetByOfficialCodeAsync("SVP", "en", CancellationToken.None);
        await provider.GetSetCardAsync("svp", "216", "en", CancellationToken.None);
        await provider.GetSetCardAsync("mep", "011", "en", CancellationToken.None);
        await provider.GetSetCardAsync("ssp", "132", "en", CancellationToken.None);

        Assert.Contains(paths, path => path.Contains("/sets?abbreviation.official=SVP", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.EndsWith("/sets/svp/216", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.EndsWith("/sets/mep/011", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.EndsWith("/sets/ssp/132", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PokemonTcgIo_SendsPerRequestKeyAndParsesEditionBuckets()
    {
        const string json = """
        {"data":{"id":"base2-1","name":"Clefable","number":"1","set":{"id":"base2","name":"Jungle"},
          "nationalPokedexNumbers":[36],"images":{"small":"small.png","large":"large.png"},
          "cardmarket":{"url":"https://cardmarket.example","updatedAt":"2026/08/01","prices":{"trendPrice":36.55}},
          "tcgplayer":{"url":"https://tcgplayer.example","updatedAt":"2026/08/01","prices":{
            "1stEditionHolofoil":{"market":122.03},"unlimitedHolofoil":{"market":36.51}
          }}}}
        """;
        string? sentKey = null;
        var provider = new PokemonTcgIoProvider(Factory(json, request =>
            sentKey = request.Headers.GetValues("X-Api-Key").Single()));

        var card = await provider.GetCardAsync("base2-1", "user-specific-key", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("user-specific-key", sentKey);
        Assert.Contains("1st-edition-holo", card.Variants);
        Assert.Contains("unlimited-holo", card.Variants);
        Assert.Equal(36.55m, card.PriceEur);
        Assert.Equal(122.03m, card.VariantPricesUsd["1st-edition-holo"]);
    }

    [Fact]
    public async Task PokemonTcgIo_NullPriceBucketsAndNullableLatest_DoNotThrow()
    {
        const string json = """
        {"data":{"id":"svp-216","name":"Promo","number":"216","set":{"id":"svp","name":"Promos"},
          "nationalPokedexNumbers":[null,"25"],
          "cardmarket":{"updatedAt":"2026-08-01T08:03:04Z","prices":{"trendPrice":null,"lowPrice":null}},
          "tcgplayer":{"updatedAt":null,"prices":{"normal":{"market":null},"reverseHolofoil":null}}}}
        """;
        var provider = new PokemonTcgIoProvider(Factory(json));

        var card = await provider.GetCardAsync("svp-216", "key", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Null(card.PriceEur);
        Assert.Null(card.PriceUsd);
        Assert.NotNull(card.PriceUpdatedAt);
        Assert.Equal(2026, card.PriceUpdatedAt.Value.Year);
    }

    [Fact]
    public void BriefMerge_PreservesDetailedPricesVariantsAndImages()
    {
        var entity = new TcgCardEntity
        {
            ProviderCardId = "svp-216",
            Name = "Detailed Promo",
            Number = "216",
            ImageSmall = "https://images.pokemontcg.io/svp/216.png",
            ImageLarge = "https://images.pokemontcg.io/svp/216_hires.png",
            VariantsJson = "[\"normal\",\"reverse\"]",
            VariantPricesEurJson = "{\"reverse\":12.5}",
            VariantPricesUsdJson = "{\"reverse\":14.5}",
            PriceEur = 12.5m,
            PriceUsd = 14.5m,
            DetailedAt = DateTime.UtcNow
        };
        var brief = new TcgProviderCard(
            "svp-216", "svp", "Promos", "Promo", "216", null, null, null, null,
            [], ["normal"], null, null,
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(),
            null, null, null, IsComplete: false);
        var apply = typeof(TcgCollectionService).GetMethod("ApplyCard", BindingFlags.NonPublic | BindingFlags.Static)!;

        apply.Invoke(null, [entity, brief, null]);

        Assert.Equal(12.5m, entity.PriceEur);
        Assert.Equal(14.5m, entity.PriceUsd);
        Assert.Contains("reverse", entity.VariantsJson);
        Assert.Contains("reverse", entity.VariantPricesEurJson);
        Assert.NotNull(entity.ImageLarge);
        Assert.NotNull(entity.DetailedAt);
    }

    [Theory]
    [InlineData("216", "216")]
    [InlineData("011", "11")]
    [InlineData("132/191", "0132")]
    public void CollectorComparison_IsNumericWithoutChangingDisplayValues(string stored, string requested)
    {
        var compare = typeof(TcgCollectionService).GetMethod(
            "CollectorNumbersEqual",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var equal = (bool)compare.Invoke(null, [stored, requested])!;

        Assert.True(equal);
    }

    [Theory]
    [InlineData("216", null, "216", null)]
    [InlineData("011", null, "011", null)]
    [InlineData("132/191", null, "132", 191)]
    [InlineData("SVP 216", "SVP", "216", null)]
    [InlineData("MEP 011", "MEP", "011", null)]
    [InlineData("SSP 132/191", "SSP", "132", 191)]
    public void CollectorReference_ParsesPrintedFormats(
        string input,
        string? expectedCode,
        string expectedLocalId,
        int? expectedTotal)
    {
        var parse = typeof(TcgCollectionService).GetMethod(
            "TryParseCollectorReference",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [input, null];

        var parsed = (bool)parse.Invoke(null, arguments)!;
        var reference = arguments[1]!;

        Assert.True(parsed);
        Assert.Equal(expectedCode, (string?)reference.GetType().GetProperty("OfficialCode")!.GetValue(reference));
        Assert.Equal(expectedLocalId, (string?)reference.GetType().GetProperty("LocalId")!.GetValue(reference));
        Assert.Equal(expectedTotal, (int?)reference.GetType().GetProperty("PrintedTotal")!.GetValue(reference));
    }

    private static IHttpClientFactory Factory(string responseJson, Action<HttpRequestMessage>? inspect = null)
    {
        var client = new HttpClient(new StubHandler(request =>
        {
            inspect?.Invoke(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://provider.example/")
        };
        return new StubClientFactory(client);
    }

    private sealed class StubClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
