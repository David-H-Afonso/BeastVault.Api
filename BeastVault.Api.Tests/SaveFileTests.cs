using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class SaveFileTests : IClassFixture<HouseholdApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HouseholdApiFactory _factory;
    private readonly HttpClient _client;

    public SaveFileTests(HouseholdApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SaveEndpoints_AreUserScoped_AndDownloadPreservesOriginalBytes()
    {
        var userA = await RegisterAsync($"save-a-{Guid.NewGuid():N}");
        var userB = await RegisterAsync($"save-b-{Guid.NewGuid():N}");
        var saveBytes = new byte[] { 0x42, 0x45, 0x41, 0x53, 0x54 };
        int saveId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var save = new SaveFileEntity
            {
                UserId = userA.UserId,
                Sha256 = Guid.NewGuid().ToString("N"),
                FileName = "main",
                OriginalFileName = "main",
                Format = "main",
                Size = saveBytes.Length,
                StoredPath = "not-used/main",
                RawBlob = saveBytes,
                Generation = 9,
                OriginGame = 51,
                GameName = "Violet",
                SaveType = "9SV",
                ChecksumsValid = true,
                Trainer = new SaveTrainerEntity
                {
                    TrainerName = "Juliana",
                    TrainerId = 123456,
                    SecretId = 1234,
                    Gender = 1,
                    Language = "SPA",
                    PlayTimeHours = 42,
                    DexSeen = 100,
                    DexCaught = 75
                },
                PokedexEntries =
                [
                    new SavePokedexEntryEntity
                    {
                        SpeciesId = 25,
                        SpeciesName = "Pikachu",
                        Seen = true,
                        Caught = true
                    }
                ],
                PokemonPreviews =
                [
                    new SavePokemonPreviewEntity
                    {
                        Location = SavePokemonLocation.Party,
                        SlotIndex = 0,
                        SpeciesId = 25,
                        SpeciesName = "Pikachu",
                        Level = 20,
                        NatureName = "Jolly",
                        AbilityName = "Static",
                        HeldItemName = "None",
                        MovesJson = "[]",
                        PokemonHash = Guid.NewGuid().ToString("N"),
                        PokemonStoredHash = Guid.NewGuid().ToString("N")
                    }
                ]
            };
            db.SaveFiles.Add(save);
            await db.SaveChangesAsync();
            saveId = save.Id;
        }

        var listA = await SendAsync(HttpMethod.Get, "/saves", userA.Token);
        listA.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await listA.Content.ReadAsStringAsync()))
        {
            var save = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(saveId, save.GetProperty("id").GetInt32());
            Assert.Equal("main", save.GetProperty("originalFileName").GetString());
            Assert.Equal(JsonValueKind.Null, save.GetProperty("title").ValueKind);
            Assert.Equal("Violet", save.GetProperty("displayTitle").GetString());
            Assert.Equal("Juliana", save.GetProperty("trainerName").GetString());
            Assert.Equal(1, save.GetProperty("trainerGender").GetInt32());
            Assert.Equal(18, save.GetProperty("badgeTotal").GetInt32());
            Assert.Equal(1, save.GetProperty("partyCount").GetInt32());
        }

        var detailA = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", userA.Token);
        detailA.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await detailA.Content.ReadAsStringAsync()))
        {
            Assert.Equal("SPA", json.RootElement.GetProperty("trainer").GetProperty("language").GetString());
            Assert.Single(json.RootElement.GetProperty("pokedex").EnumerateArray());
            Assert.Single(json.RootElement.GetProperty("pokemon").EnumerateArray());
        }

        var scopedUpdate = await SendAsync(
            HttpMethod.Patch,
            $"/saves/{saveId}",
            userB.Token,
            JsonContent.Create(new { title = "Not yours", notes = "Not yours" }));
        Assert.Equal(HttpStatusCode.NotFound, scopedUpdate.StatusCode);

        var afterScopedUpdate = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", userA.Token);
        afterScopedUpdate.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await afterScopedUpdate.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("summary").GetProperty("title").ValueKind);
        }

        var oversizedTitle = await SendAsync(
            HttpMethod.Patch,
            $"/saves/{saveId}",
            userA.Token,
            JsonContent.Create(new { title = new string('x', 121), notes = (string?)null }));
        Assert.Equal(HttpStatusCode.BadRequest, oversizedTitle.StatusCode);

        var oversizedNotes = await SendAsync(
            HttpMethod.Patch,
            $"/saves/{saveId}",
            userA.Token,
            JsonContent.Create(new { title = (string?)null, notes = new string('x', 4001) }));
        Assert.Equal(HttpStatusCode.BadRequest, oversizedNotes.StatusCode);

        var update = await SendAsync(
            HttpMethod.Patch,
            $"/saves/{saveId}",
            userA.Token,
            JsonContent.Create(new { title = "  Violet living dex  ", notes = "  Complete the dex  " }));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var updatedDetail = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", userA.Token);
        updatedDetail.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await updatedDetail.Content.ReadAsStringAsync()))
        {
            var summary = json.RootElement.GetProperty("summary");
            Assert.Equal("Violet living dex", summary.GetProperty("title").GetString());
            Assert.Equal("Violet living dex", summary.GetProperty("displayTitle").GetString());
            Assert.Equal("Complete the dex", summary.GetProperty("notes").GetString());
        }

        var clearTitle = await SendAsync(
            HttpMethod.Patch,
            $"/saves/{saveId}",
            userA.Token,
            JsonContent.Create(new { title = "   ", notes = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, clearTitle.StatusCode);

        var clearedDetail = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", userA.Token);
        clearedDetail.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await clearedDetail.Content.ReadAsStringAsync()))
        {
            var summary = json.RootElement.GetProperty("summary");
            Assert.Equal(JsonValueKind.Null, summary.GetProperty("title").ValueKind);
            Assert.Equal("Violet", summary.GetProperty("displayTitle").GetString());
        }

        var downloadA = await SendAsync(HttpMethod.Get, $"/saves/{saveId}/download", userA.Token);
        Assert.Equal(HttpStatusCode.OK, downloadA.StatusCode);
        Assert.Equal(saveBytes, await downloadA.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/saves/{saveId}", userB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Get, $"/saves/{saveId}/download", userB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Delete, $"/saves/{saveId}", userB.Token)).StatusCode);
    }

    [Fact]
    public async Task Detail_RebuildsPokedexFromTheRawSave()
    {
        var user = await RegisterAsync($"save-grouped-{Guid.NewGuid():N}");
        var blankSave = (SAV1)BlankSaveFile.Get(GameVersion.RD, "GROUPED", LanguageID.English);
        blankSave.Data[0x2F2D] = 0xFF;
        blankSave.Data[0x30C1] = 0xFF;
        var saveBytes = blankSave.Write().ToArray();
        int saveId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var save = new SaveFileEntity
            {
                UserId = user.UserId,
                Sha256 = Guid.NewGuid().ToString("N"),
                FileName = "red.sav",
                OriginalFileName = "red.sav",
                Format = "sav",
                Size = saveBytes.Length,
                StoredPath = "not-used/red.sav",
                RawBlob = saveBytes,
                Generation = 1,
                OriginGame = 35,
                GameName = "Red",
                SaveType = "1",
                ChecksumsValid = true,
                Trainer = new SaveTrainerEntity { TrainerName = "GROUPED", Language = "ENG" }
            };
            db.SaveFiles.Add(save);
            await db.SaveChangesAsync();
            saveId = save.Id;
        }

        var response = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", user.Token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(151, json.RootElement.GetProperty("nationalPokedex").GetProperty("total").GetInt32());
        Assert.Equal(151, json.RootElement.GetProperty("regionalPokedex").GetProperty("total").GetInt32());
        Assert.Equal(151, json.RootElement.GetProperty("pokedex").GetArrayLength());
    }

    [Fact]
    public async Task Upload_RejectsAnUnrecognizedFileWithoutPersistingIt()
    {
        var user = await RegisterAsync($"save-invalid-{Guid.NewGuid():N}");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3, 4]), "files", "invalid.sav");

        var response = await SendAsync(HttpMethod.Post, "/saves", user.Token, form);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("error", result.GetProperty("status").GetString());
        Assert.Contains("PKHeX", result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Upload_AndSelectiveImport_RoundTripsPokemonIntoTheVault()
    {
        var user = await RegisterAsync($"save-import-{Guid.NewGuid():N}");
        var save = BlankSaveFile.Get(GameVersion.B, "HILBERT", LanguageID.English);
        save.TID16 = 12345;
        save.SID16 = 54321;
        var pokemon = save.BlankPKM;
        pokemon.Species = 25;
        pokemon.CurrentLevel = 5;
        pokemon.OriginalTrainerName = save.OT;
        pokemon.TID16 = save.TID16;
        pokemon.SID16 = save.SID16;
        pokemon.Language = save.Language;
        pokemon.Version = save.Version;
        save.SetPartySlotAtIndex(pokemon, 0, EntityImportSettings.None);
        var saveBytes = save.Write().ToArray();

        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent(saveBytes), "files", "pokemon-black.dsv");
        var uploadResponse = await SendAsync(HttpMethod.Post, "/saves", user.Token, upload);
        uploadResponse.EnsureSuccessStatusCode();
        using var uploadJson = JsonDocument.Parse(await uploadResponse.Content.ReadAsStringAsync());
        var uploadResult = Assert.Single(uploadJson.RootElement.EnumerateArray());
        Assert.Equal("imported", uploadResult.GetProperty("status").GetString());
        var saveId = uploadResult.GetProperty("saveFileId").GetInt32();

        var downloadResponse = await SendAsync(HttpMethod.Get, $"/saves/{saveId}/download", user.Token);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal(saveBytes, await downloadResponse.Content.ReadAsByteArrayAsync());

        var detailResponse = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", user.Token);
        detailResponse.EnsureSuccessStatusCode();
        using var detailJson = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var preview = Assert.Single(detailJson.RootElement.GetProperty("pokemon").EnumerateArray());
        Assert.Equal(25, preview.GetProperty("speciesId").GetInt32());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("existingPokemonId").ValueKind);
        var previewId = preview.GetProperty("id").GetInt32();

        var importResponse = await SendAsync(
            HttpMethod.Post,
            $"/saves/{saveId}/import",
            user.Token,
            JsonContent.Create(new { previewIds = new[] { previewId } }));
        importResponse.EnsureSuccessStatusCode();
        using var importJson = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());
        var importResult = Assert.Single(importJson.RootElement.EnumerateArray());
        Assert.Equal("imported", importResult.GetProperty("status").GetString());
        Assert.True(importResult.GetProperty("pokemonId").GetInt32() > 0);

        var repeatResponse = await SendAsync(
            HttpMethod.Post,
            $"/saves/{saveId}/import",
            user.Token,
            JsonContent.Create(new { previewIds = new[] { previewId } }));
        repeatResponse.EnsureSuccessStatusCode();
        using var repeatJson = JsonDocument.Parse(await repeatResponse.Content.ReadAsStringAsync());
        Assert.Equal("duplicate", Assert.Single(repeatJson.RootElement.EnumerateArray()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Detail_HandlesLargePokedexAndPreviewCollectionsWithoutCartesianMaterialization()
    {
        var user = await RegisterAsync($"save-large-{Guid.NewGuid():N}");
        int saveId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saveKey = Guid.NewGuid().ToString("N");
            var save = new SaveFileEntity
            {
                UserId = user.UserId,
                Sha256 = saveKey,
                FileName = "pokemon-black.dsv",
                OriginalFileName = "pokemon-black.dsv",
                Format = "dsv",
                Size = 1,
                StoredPath = "not-used/pokemon-black.dsv",
                RawBlob = [0x42],
                Generation = 5,
                OriginGame = (int)GameVersion.B,
                GameName = "Black",
                SaveType = "5",
                ChecksumsValid = true,
                Trainer = new SaveTrainerEntity
                {
                    TrainerName = "Hilbert",
                    TrainerId = 12345,
                    SecretId = 54321,
                    Gender = 0,
                    Language = "ENG",
                    DexSeen = 1025,
                    DexCaught = 900
                },
                PokedexEntries = Enumerable.Range(1, 1025)
                    .Select(speciesId => new SavePokedexEntryEntity
                    {
                        SpeciesId = speciesId,
                        SpeciesName = $"Species {speciesId}",
                        Seen = true,
                        Caught = speciesId <= 900
                    })
                    .ToList(),
                PokemonPreviews = Enumerable.Range(0, 138)
                    .Select(index =>
                    {
                        var isParty = index < 6;
                        var boxSlot = index - 6;
                        return new SavePokemonPreviewEntity
                        {
                            Location = isParty ? SavePokemonLocation.Party : SavePokemonLocation.Box,
                            BoxIndex = isParty ? null : boxSlot / 30,
                            SlotIndex = isParty ? index : boxSlot % 30,
                            SpeciesId = index + 1,
                            SpeciesName = $"Species {index + 1}",
                            Level = 50,
                            NatureName = "Hardy",
                            AbilityName = "None",
                            HeldItemName = "None",
                            MovesJson = "[]",
                            PokemonHash = $"party-{saveKey}-{index}",
                            PokemonStoredHash = $"stored-{saveKey}-{index}"
                        };
                    })
                    .ToList()
            };
            db.SaveFiles.Add(save);
            await db.SaveChangesAsync();
            saveId = save.Id;
        }

        var response = await SendAsync(HttpMethod.Get, $"/saves/{saveId}", user.Token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = json.RootElement.GetProperty("summary");
        var pokedex = json.RootElement.GetProperty("pokedex");
        var pokemon = json.RootElement.GetProperty("pokemon");

        Assert.Equal(6, summary.GetProperty("partyCount").GetInt32());
        Assert.Equal(132, summary.GetProperty("storedPokemonCount").GetInt32());
        Assert.Equal(8, summary.GetProperty("badgeTotal").GetInt32());
        Assert.Equal(1025, pokedex.GetArrayLength());
        Assert.Equal(1025, pokedex[1024].GetProperty("speciesId").GetInt32());
        Assert.Equal(138, pokemon.GetArrayLength());
        Assert.Equal("party", pokemon[0].GetProperty("location").GetString());
        Assert.Equal("box", pokemon[137].GetProperty("location").GetString());
        Assert.Equal(138, pokemon[137].GetProperty("speciesId").GetInt32());
    }

    private async Task<LoginResponse> RegisterAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            username,
            password = "Test-password-123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string uri,
        string token,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(int UserId, string Username, string Role, string Token);
}
