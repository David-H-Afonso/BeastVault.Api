using System.Net;
using System.Text;
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
