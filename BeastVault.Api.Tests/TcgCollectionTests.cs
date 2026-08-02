using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class TcgCollectionTests : IClassFixture<HouseholdApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HouseholdApiFactory _factory;
    private readonly HttpClient _client;

    public TcgCollectionTests(HouseholdApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ApiKey_IsEncryptedMaskedAndScopedPerUser()
    {
        var userA = await RegisterAsync(Unique("tcg-key-a"));
        var userB = await RegisterAsync(Unique("tcg-key-b"));
        const string apiKey = "free-api-key-super-secret-1234";

        var update = await SendAsync(
            HttpMethod.Patch,
            "/auth/preferences/tcg-api-key",
            userA.Token,
            JsonContent.Create(new { apiKey }));
        update.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await update.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("configured").GetBoolean());
            Assert.Equal("••••1234", json.RootElement.GetProperty("maskedApiKey").GetString());
            Assert.DoesNotContain(apiKey, json.RootElement.GetRawText());
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.UserApiCredentials.AsNoTracking().SingleAsync(x => x.UserId == userA.UserId);
            Assert.NotEqual(apiKey, stored.ProtectedValue);
            Assert.DoesNotContain(apiKey, stored.ProtectedValue);
            Assert.Equal("1234", stored.LastFour);
        }

        var statusB = await SendAsync(HttpMethod.Get, "/auth/preferences/tcg-api-key", userB.Token);
        statusB.EnsureSuccessStatusCode();
        using var statusJson = JsonDocument.Parse(await statusB.Content.ReadAsStringAsync());
        Assert.False(statusJson.RootElement.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task Collection_AccumulatesCopiesAndCalculatesDexSetAndValueProgress()
    {
        var userA = await RegisterAsync(Unique("tcg-owner-a"));
        var userB = await RegisterAsync(Unique("tcg-owner-b"));
        var (setId, charmanderId, _) = await SeedCatalogAsync();

        var first = await AddAsync(userA.Token, charmanderId, 2);
        var entryId = first.GetProperty("id").GetInt32();
        Assert.Equal(2, first.GetProperty("quantity").GetInt32());

        var incremented = await AddAsync(userA.Token, charmanderId, 3);
        Assert.Equal(entryId, incremented.GetProperty("id").GetInt32());
        Assert.Equal(5, incremented.GetProperty("quantity").GetInt32());

        var collection = await SendAsync(HttpMethod.Get, "/tcg/collection", userA.Token);
        collection.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await collection.Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            Assert.Equal("Charmander", item.GetProperty("card").GetProperty("name").GetString());
            Assert.Equal(50m, item.GetProperty("totalValueEur").GetDecimal());
            var entry = Assert.Single(item.GetProperty("entries").EnumerateArray());
            Assert.Equal("ES", entry.GetProperty("language").GetString());
        }

        var stats = await SendAsync(HttpMethod.Get, "/tcg/collection/stats", userA.Token);
        stats.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await stats.Content.ReadAsStringAsync()))
        {
            var root = json.RootElement;
            Assert.Equal(1, root.GetProperty("uniqueCards").GetInt32());
            Assert.Equal(5, root.GetProperty("totalCopies").GetInt32());
            Assert.Equal(50m, root.GetProperty("totalValueEur").GetDecimal());
            Assert.Equal(1, root.GetProperty("national").GetProperty("owned").GetInt32());
            var kanto = root.GetProperty("regions").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Kanto");
            var unova = root.GetProperty("regions").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Unova");
            Assert.Equal(1, kanto.GetProperty("owned").GetInt32());
            Assert.Equal(0, unova.GetProperty("owned").GetInt32());
            var set = Assert.Single(root.GetProperty("sets").EnumerateArray());
            Assert.Equal(setId, set.GetProperty("setId").GetInt32());
            Assert.Equal(1, set.GetProperty("owned").GetInt32());
            Assert.Equal(2, set.GetProperty("total").GetInt32());
        }

        var sets = await SendAsync(HttpMethod.Get, "/tcg/sets", userA.Token);
        sets.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await sets.Content.ReadAsStringAsync()))
        {
            var set = Assert.Single(json.RootElement.EnumerateArray(), x => x.GetProperty("id").GetInt32() == setId);
            Assert.Equal(1, set.GetProperty("ownedUniqueCards").GetInt32());
            Assert.Equal(5, set.GetProperty("ownedCopies").GetInt32());
            Assert.Equal(50m, set.GetProperty("completionPercent").GetDecimal());
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Delete, $"/tcg/collection/{entryId}", userB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(
                HttpMethod.Patch,
                $"/tcg/collection/{entryId}",
                userB.Token,
                JsonContent.Create(new { quantity = 99 }))).StatusCode);
        var collectionB = await SendAsync(HttpMethod.Get, "/tcg/collection", userB.Token);
        collectionB.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await collectionB.Content.ReadAsStringAsync()))
            Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
        var statsB = await SendAsync(HttpMethod.Get, "/tcg/collection/stats", userB.Token);
        statsB.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await statsB.Content.ReadAsStringAsync()))
            Assert.Equal(0, json.RootElement.GetProperty("uniqueCards").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent,
            (await SendAsync(HttpMethod.Delete, $"/tcg/collection/{entryId}", userA.Token)).StatusCode);
    }

    [Fact]
    public async Task Collection_PagesUniqueCardsAndReturnsEveryOwnedEntry()
    {
        var user = await RegisterAsync(Unique("tcg-grouped"));
        var (_, charmanderId, reshiramId) = await SeedCatalogAsync();
        await AddAsync(user.Token, charmanderId, 2, "normal", "NM", "ES");
        await AddAsync(user.Token, charmanderId, 1, "reverse", "LP", "EN");
        await AddAsync(user.Token, reshiramId, 1);

        var firstResponse = await SendAsync(HttpMethod.Get, "/tcg/collection?page=1&pageSize=1", user.Token);
        var secondResponse = await SendAsync(HttpMethod.Get, "/tcg/collection?page=2&pageSize=1", user.Token);
        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        using var second = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());

        Assert.Equal(2, first.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, second.RootElement.GetProperty("totalCount").GetInt32());
        var groups = first.RootElement.GetProperty("items").EnumerateArray()
            .Concat(second.RootElement.GetProperty("items").EnumerateArray())
            .ToList();
        Assert.Equal(2, groups.Count);
        var charmander = Assert.Single(groups, item => item.GetProperty("card").GetProperty("name").GetString() == "Charmander");
        Assert.Equal(2, charmander.GetProperty("entries").GetArrayLength());
        Assert.Equal(3, charmander.GetProperty("totalCopies").GetInt32());
        Assert.Equal(20m, charmander.GetProperty("totalValueEur").GetDecimal());
    }

    [Fact]
    public async Task Add_SparseCardDoesNotRequireProviderRefresh()
    {
        var user = await RegisterAsync(Unique("tcg-sparse"));
        var (_, charmanderId, _) = await SeedCatalogAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var card = await db.TcgCards.SingleAsync(x => x.Id == charmanderId);
            card.DetailedAt = null;
            card.PriceEur = null;
            card.PriceUsd = null;
            card.ImageSmall = null;
            card.ImageLarge = null;
            card.PriceCheckedAt = DateTime.UtcNow;
            card.LastRefreshError = "Provider unavailable.";
            await db.SaveChangesAsync();
        }

        var added = await AddAsync(user.Token, charmanderId, 1);

        Assert.Equal(charmanderId, added.GetProperty("card").GetProperty("id").GetInt32());
        Assert.Equal(1, added.GetProperty("quantity").GetInt32());
        Assert.Equal("Provider unavailable.", added.GetProperty("card").GetProperty("lastRefreshError").GetString());
    }

    [Fact]
    public async Task BatchDelete_IsScopedToTheAuthenticatedUser()
    {
        var userA = await RegisterAsync(Unique("tcg-batch-a"));
        var userB = await RegisterAsync(Unique("tcg-batch-b"));
        var (_, charmanderId, _) = await SeedCatalogAsync();
        await AddAsync(userA.Token, charmanderId, 1, "normal", "NM", "ES");
        await AddAsync(userA.Token, charmanderId, 1, "reverse", "LP", "EN");
        await AddAsync(userB.Token, charmanderId, 4, "normal", "NM", "ES");

        var deleted = await SendAsync(
            HttpMethod.Delete,
            "/tcg/collection/cards",
            userA.Token,
            JsonContent.Create(new { cardIds = new[] { charmanderId } }));
        deleted.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await deleted.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, json.RootElement.GetProperty("deletedCards").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("deletedEntries").GetInt32());
        }

        var collectionA = await SendAsync(HttpMethod.Get, "/tcg/collection", userA.Token);
        var collectionB = await SendAsync(HttpMethod.Get, "/tcg/collection", userB.Token);
        using var jsonA = JsonDocument.Parse(await collectionA.Content.ReadAsStringAsync());
        using var jsonB = JsonDocument.Parse(await collectionB.Content.ReadAsStringAsync());
        Assert.Equal(0, jsonA.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, jsonB.RootElement.GetProperty("totalCount").GetInt32());
        var ownedByB = Assert.Single(jsonB.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(4, ownedByB.GetProperty("totalCopies").GetInt32());
    }

    [Fact]
    public async Task CardDto_UsesLocalAssetUrlAndDisallowedSourceReturnsNotFound()
    {
        var user = await RegisterAsync(Unique("tcg-assets"));
        var (_, charmanderId, _) = await SeedCatalogAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var card = await db.TcgCards.SingleAsync(x => x.Id == charmanderId);
            card.ImageSmall = "https://untrusted.example/card.webp";
            await db.SaveChangesAsync();
        }

        var cardResponse = await SendAsync(HttpMethod.Get, $"/tcg/cards/{charmanderId}", user.Token);
        cardResponse.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await cardResponse.Content.ReadAsStringAsync()))
        {
            Assert.Equal($"/tcg/assets/cards/{charmanderId}/small", json.RootElement.GetProperty("imageSmall").GetString());
        }

        var assetResponse = await _client.GetAsync($"/tcg/assets/cards/{charmanderId}/small");
        Assert.Equal(HttpStatusCode.NotFound, assetResponse.StatusCode);
    }

    private async Task<JsonElement> AddAsync(
        string token,
        int cardId,
        int quantity,
        string variant = "normal",
        string condition = "NM",
        string language = "ES")
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/tcg/collection",
            token,
            JsonContent.Create(new
            {
                cardId,
                variant,
                condition,
                language,
                quantity,
                notes = "Binder"
            }));
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task<(int SetId, int CharmanderId, int ReshiramId)> SeedCatalogAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var providerSetId = Unique("test-set");
        var set = new TcgSetEntity
        {
            ProviderSetId = providerSetId,
            Name = "Test Collection",
            NameEn = "Test Collection",
            PrintedTotal = 2,
            Total = 2,
            SyncedAt = DateTime.UtcNow,
            CardsSyncedAt = DateTime.UtcNow
        };
        db.TcgSets.Add(set);
        await db.SaveChangesAsync();

        var charmander = NewCard(set.Id, $"{providerSetId}-1", "Charmander", "1", 4, 10m, 12m);
        var reshiram = NewCard(set.Id, $"{providerSetId}-2", "Reshiram", "2", 643, 20m, 22m);
        db.TcgCards.AddRange(charmander, reshiram);
        await db.SaveChangesAsync();
        return (set.Id, charmander.Id, reshiram.Id);
    }

    private static TcgCardEntity NewCard(
        int setId,
        string providerId,
        string name,
        string number,
        int speciesId,
        decimal eur,
        decimal usd) => new()
    {
        SetId = setId,
        ProviderCardId = providerId,
        Name = name,
        NameEn = name,
        Number = number,
        NationalPokedexNumbersJson = $"[{speciesId}]",
        VariantsJson = "[\"normal\",\"reverse\"]",
        VariantPricesEurJson = $"{{\"normal\":{eur}}}",
        VariantPricesUsdJson = $"{{\"normal\":{usd}}}",
        PriceEur = eur,
        PriceUsd = usd,
        PriceUpdatedAt = DateTime.UtcNow,
        DetailedAt = DateTime.UtcNow,
        SyncedAt = DateTime.UtcNow
    };

    private async Task<LoginResponse> RegisterAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { username, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
