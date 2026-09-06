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

public sealed class DexHuntTests : IClassFixture<HouseholdApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HouseholdApiFactory _factory;
    private readonly HttpClient _client;

    public DexHuntTests(HouseholdApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CrudCompletionFilteringAndOwnership_AreIndependentFromVaultPokemon()
    {
        await SeedSpeciesAsync();
        var owner = await RegisterAsync(Unique("hunt-owner"));
        var other = await RegisterAsync(Unique("hunt-other"));

        var create = await SendAsync(HttpMethod.Post, "/dex-hunts", owner.Token,
            JsonContent.Create(new CreateDexHuntListRequest("Paldea gaps", 50, "Finish the regional dex")));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var list = (await create.Content.ReadFromJsonAsync<DexHuntListSummaryDto>(JsonOptions))!;
        Assert.Equal("Scarlet", list.GameName);

        var first = await AddAsync(owner.Token, list.Id, 1, 2, "Starter trade");
        var second = await AddAsync(owner.Token, list.Id, 25, 1, null);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);

        var caught = await SendAsync(HttpMethod.Patch, $"/dex-hunts/{list.Id}/items/{second.Id}", owner.Token,
            JsonContent.Create(new { isCaught = true, priority = 0, notes = "Caught in Area Zero" }));
        Assert.Equal(HttpStatusCode.NoContent, caught.StatusCode);

        var open = await SendAsync(HttpMethod.Get, $"/dex-hunts/{list.Id}?status=open&priority=2&search=starter", owner.Token);
        open.EnsureSuccessStatusCode();
        var openDetail = (await open.Content.ReadFromJsonAsync<DexHuntListDetailDto>(JsonOptions))!;
        var openItem = Assert.Single(openDetail.Items);
        Assert.Equal(1, openItem.SpeciesId);
        Assert.Equal(2, openDetail.List.TotalCount);
        Assert.Equal(1, openDetail.List.CaughtCount);

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/dex-hunts/{list.Id}", other.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Patch, $"/dex-hunts/{list.Id}/items/{first.Id}", other.Token,
                JsonContent.Create(new { isCaught = true, priority = 1, notes = (string?)null }))).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Pokemon.AnyAsync(pokemon => pokemon.UserId == owner.UserId));
    }

    [Fact]
    public async Task Reorder_RequiresEveryOwnedIdAndPersistsAtomically()
    {
        await SeedSpeciesAsync();
        var user = await RegisterAsync(Unique("hunt-order"));
        var list = await CreateAsync(user.Token, "Order test", 51);
        var first = await AddAsync(user.Token, list.Id, 1, 1, null);
        var second = await AddAsync(user.Token, list.Id, 25, 1, null);

        var invalid = await SendAsync(HttpMethod.Put, $"/dex-hunts/{list.Id}/items/reorder", user.Token,
            JsonContent.Create(new { itemIds = new[] { second.Id } }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var reordered = await SendAsync(HttpMethod.Put, $"/dex-hunts/{list.Id}/items/reorder", user.Token,
            JsonContent.Create(new { itemIds = new[] { second.Id, first.Id } }));
        Assert.Equal(HttpStatusCode.NoContent, reordered.StatusCode);

        var detail = await GetAsync(user.Token, list.Id);
        Assert.Equal(new[] { second.Id, first.Id }, detail.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task ExportImport_RoundTripsPortableDataAndRejectsInvalidFiles()
    {
        await SeedSpeciesAsync();
        var user = await RegisterAsync(Unique("hunt-transfer"));
        var list = await CreateAsync(user.Token, "Trade targets", 50);
        var item = await AddAsync(user.Token, list.Id, 25, 2, "Friend safari");
        await SendAsync(HttpMethod.Patch, $"/dex-hunts/{list.Id}/items/{item.Id}", user.Token,
            JsonContent.Create(new { isCaught = true, priority = 2, notes = "Friend safari" }));

        var exported = await SendAsync(HttpMethod.Get, $"/dex-hunts/{list.Id}/export", user.Token);
        exported.EnsureSuccessStatusCode();
        Assert.Equal("application/json", exported.Content.Headers.ContentType?.MediaType);
        var payload = (await exported.Content.ReadFromJsonAsync<DexHuntExportDto>(JsonOptions))!;
        Assert.Equal(1, payload.SchemaVersion);
        Assert.Equal("Trade targets", payload.List.Name);
        Assert.Equal(25, Assert.Single(payload.List.Items).SpeciesId);

        var imported = await SendAsync(HttpMethod.Post, "/dex-hunts/import", user.Token, JsonContent.Create(payload));
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);
        var importedList = (await imported.Content.ReadFromJsonAsync<DexHuntListSummaryDto>(JsonOptions))!;
        Assert.NotEqual(list.Id, importedList.Id);
        var importedDetail = await GetAsync(user.Token, importedList.Id);
        Assert.True(Assert.Single(importedDetail.Items).IsCaught);

        var invalidPayload = payload with
        {
            List = payload.List with
            {
                Items = [new DexHuntExportItemDto(99999, "not-real", 1, false, null, null)]
            }
        };
        var invalid = await SendAsync(HttpMethod.Post, "/dex-hunts/import", user.Token, JsonContent.Create(invalidPayload));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task DuplicateSpeciesAndUnauthenticatedAccess_AreRejected()
    {
        await SeedSpeciesAsync();
        var user = await RegisterAsync(Unique("hunt-validation"));
        var list = await CreateAsync(user.Token, "Validation", 44);
        await AddAsync(user.Token, list.Id, 1, 1, null);

        var duplicate = await SendAsync(HttpMethod.Post, $"/dex-hunts/{list.Id}/items", user.Token,
            JsonContent.Create(new { speciesId = 1, priority = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/dex-hunts")).StatusCode);
    }

    private async Task SeedSpeciesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (id, name, generation, types) in new[]
        {
            (1, "bulbasaur", 1, "[{\"name\":\"grass\"},{\"name\":\"poison\"}]"),
            (25, "pikachu", 1, "[{\"name\":\"electric\"}]")
        })
        {
            if (!await db.PokedexEntries.AnyAsync(entry => entry.SpeciesId == id))
                db.PokedexEntries.Add(new PokedexEntry { SpeciesId = id, Name = name, Generation = generation });
            if (!await db.PokedexPokemon.AnyAsync(pokemon => pokemon.PokemonId == id))
                db.PokedexPokemon.Add(new PokedexPokemon { PokemonId = id, SpeciesId = id, Name = name, IsDefault = true, Types = types });
        }
        await db.SaveChangesAsync();
    }

    private async Task<LoginResponse> RegisterAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { username, password = "VaultPass123!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    private async Task<DexHuntListSummaryDto> CreateAsync(string token, string name, int gameId)
    {
        var response = await SendAsync(HttpMethod.Post, "/dex-hunts", token,
            JsonContent.Create(new CreateDexHuntListRequest(name, gameId, null)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DexHuntListSummaryDto>(JsonOptions))!;
    }

    private async Task<DexHuntItemDto> AddAsync(string token, int listId, int speciesId, int priority, string? notes)
    {
        var response = await SendAsync(HttpMethod.Post, $"/dex-hunts/{listId}/items", token,
            JsonContent.Create(new AddDexHuntItemRequest(speciesId, priority, notes)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DexHuntItemDto>(JsonOptions))!;
    }

    private async Task<DexHuntListDetailDto> GetAsync(string token, int listId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/dex-hunts/{listId}", token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DexHuntListDetailDto>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
