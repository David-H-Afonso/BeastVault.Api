using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BeastVault.Api.Tests;

public sealed class HouseholdIntegrationTests : IClassFixture<HouseholdApiFactory>
{
    private const string RedirectUri = "http://localhost:5019/integrations/callback/provider";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HouseholdApiFactory _factory;
    private readonly HttpClient _client;

    public HouseholdIntegrationTests(HouseholdApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthorizationCode_RequiresExactRedirectAndPkce_IsSingleUse_AndRefreshReuseRevokesFamily()
    {
        var user = await RegisterAsync(Unique("pkce"));
        var verifier = new string('v', 64);

        var invalidRedirect = await AuthorizeAsync(user.Token, verifier, "https://attacker.invalid/callback");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRedirect.StatusCode);
        Assert.DoesNotContain("attacker.invalid", await invalidRedirect.Content.ReadAsStringAsync());

        var code = await CreateAuthorizationCodeAsync(user.Token, verifier);
        var wrongVerifier = await ExchangeCodeAsync(code, new string('x', 64));
        Assert.Equal(HttpStatusCode.BadRequest, wrongVerifier.StatusCode);

        var exchange = await ExchangeCodeAsync(code, verifier);
        exchange.EnsureSuccessStatusCode();
        var tokens = await exchange.Content.ReadFromJsonAsync<HouseholdTokenResponse>(JsonOptions);
        Assert.NotNull(tokens);
        Assert.Equal(900, tokens.ExpiresIn);

        var reuse = await ExchangeCodeAsync(code, verifier);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        var refresh = await _client.PostAsJsonAsync(
            "/api/integrations/household/v1/token",
            new HouseholdTokenRequest("refresh_token", "household", null, null, null, tokens.RefreshToken));
        Assert.True(refresh.IsSuccessStatusCode, await refresh.Content.ReadAsStringAsync());
        var rotated = await refresh.Content.ReadFromJsonAsync<HouseholdTokenResponse>(JsonOptions);
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        var reuseRefresh = await _client.PostAsJsonAsync(
            "/api/integrations/household/v1/token",
            new HouseholdTokenRequest("refresh_token", "household", null, null, null, tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.BadRequest, reuseRefresh.StatusCode);

        var meAfterReuse = await SendWithBearerAsync(
            HttpMethod.Get,
            "/api/integrations/household/v1/me",
            rotated.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterReuse.StatusCode);
    }

    [Fact]
    public async Task Revoke_IsIdempotent_AndRevokesOnlyIdentifiedConnection()
    {
        var user = await RegisterAsync(Unique("revoke"));
        var first = await ConnectAsync(user.Token);
        var second = await ConnectAsync(user.Token);

        var revokeRequest = new HouseholdRevokeRequest(first.RefreshToken, "refresh_token");
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("/api/integrations/household/v1/revoke", revokeRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("/api/integrations/household/v1/revoke", revokeRequest)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendWithBearerAsync(HttpMethod.Get, "/api/integrations/household/v1/me", first.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await SendWithBearerAsync(HttpMethod.Get, "/api/integrations/household/v1/me", second.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task IntegrationResponsesAreNarrow_WritesAreSplit_AndUserBDataIsNotReachable()
    {
        var userA = await RegisterAsync(Unique("user-a"));
        var userB = await RegisterAsync(Unique("user-b"));
        var (pokemonA, pokemonB) = await SeedPokemonAsync(userA.UserId, userB.UserId);
        var tokens = await ConnectAsync(userA.Token);

        var listResponse = await SendWithBearerAsync(HttpMethod.Get, "/pokemon?skip=0&take=50", tokens.AccessToken);
        Assert.True(listResponse.IsSuccessStatusCode, await listResponse.Content.ReadAsStringAsync());
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var items = listJson.RootElement.GetProperty("items");
        Assert.Single(items.EnumerateArray());
        Assert.Equal(pokemonA, items[0].GetProperty("id").GetInt32());
        Assert.False(items[0].TryGetProperty("ballId", out _));
        Assert.False(items[0].TryGetProperty("tags", out _));

        var ownDetail = await SendWithBearerAsync(HttpMethod.Get, $"/pokemon/{pokemonA}", tokens.AccessToken);
        ownDetail.EnsureSuccessStatusCode();
        using var detailJson = JsonDocument.Parse(await ownDetail.Content.ReadAsStringAsync());
        Assert.False(detailJson.RootElement.TryGetProperty("otName", out _));
        Assert.False(detailJson.RootElement.TryGetProperty("tid", out _));
        Assert.False(detailJson.RootElement.TryGetProperty("stats", out _));

        Assert.Equal(HttpStatusCode.NotFound,
            (await SendWithBearerAsync(HttpMethod.Get, $"/pokemon/{pokemonB}", tokens.AccessToken)).StatusCode);

        var genericPatch = await SendWithBearerAsync(
            HttpMethod.Patch,
            $"/pokemon/{pokemonA}",
            tokens.AccessToken,
            JsonContent.Create(new { favorite = true, notes = "not allowed" }));
        Assert.Equal(HttpStatusCode.Unauthorized, genericPatch.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await SendWithBearerAsync(
                HttpMethod.Patch,
                $"/pokemon/{pokemonA}/favorite",
                tokens.AccessToken,
                JsonContent.Create(new { favorite = true }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await SendWithBearerAsync(
                HttpMethod.Patch,
                $"/pokemon/{pokemonA}/notes",
                tokens.AccessToken,
                JsonContent.Create(new { notes = "Household note" }))).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedA = await db.Pokemon.AsNoTracking().SingleAsync(item => item.Id == pokemonA);
        var untouchedB = await db.Pokemon.AsNoTracking().SingleAsync(item => item.Id == pokemonB);
        Assert.True(updatedA.Favorite);
        Assert.Equal("Household note", updatedA.Notes);
        Assert.False(untouchedB.Favorite);
        Assert.Equal("private-b", untouchedB.Notes);
    }

    [Fact]
    public async Task ScopesAreEnforcedIndependently()
    {
        var userA = await RegisterAsync(Unique("scoped-a"));
        var userB = await RegisterAsync(Unique("scoped-b"));
        var (pokemonA, _) = await SeedPokemonAsync(userA.UserId, userB.UserId);
        var tokens = await ConnectAsync(userA.Token, ["pokemon.favorite.write"]);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendWithBearerAsync(HttpMethod.Get, "/pokemon/summary", tokens.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendWithBearerAsync(HttpMethod.Get, "/api/integrations/household/v1/me", tokens.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendWithBearerAsync(
                HttpMethod.Patch,
                $"/pokemon/{pokemonA}/notes",
                tokens.AccessToken,
                JsonContent.Create(new { notes = "denied" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await SendWithBearerAsync(
                HttpMethod.Patch,
                $"/pokemon/{pokemonA}/favorite",
                tokens.AccessToken,
                JsonContent.Create(new { favorite = true }))).StatusCode);
    }

    private async Task<LoginResponse> RegisterAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { username, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    private async Task<HouseholdTokenResponse> ConnectAsync(
        string normalJwt,
        IReadOnlyList<string>? scopes = null)
    {
        var verifier = new string('p', 64);
        var code = await CreateAuthorizationCodeAsync(normalJwt, verifier, scopes);
        var exchange = await ExchangeCodeAsync(code, verifier);
        exchange.EnsureSuccessStatusCode();
        return (await exchange.Content.ReadFromJsonAsync<HouseholdTokenResponse>(JsonOptions))!;
    }

    private async Task<string> CreateAuthorizationCodeAsync(
        string normalJwt,
        string verifier,
        IReadOnlyList<string>? scopes = null)
    {
        var response = await AuthorizeAsync(normalJwt, verifier, RedirectUri, scopes);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var authorization = await response.Content.ReadFromJsonAsync<HouseholdAuthorizeResponse>(JsonOptions);
        Assert.NotNull(authorization);
        var query = new Uri(authorization.RedirectUri).Query;
        return ParseQuery(query)["code"];
    }

    private async Task<HttpResponseMessage> AuthorizeAsync(
        string normalJwt,
        string verifier,
        string redirectUri,
        IReadOnlyList<string>? scopes = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/household/v1/authorize")
        {
            Content = JsonContent.Create(new HouseholdAuthorizeRequest(
                "household",
                redirectUri,
                "state-value",
                CreateChallenge(verifier),
                "S256",
                scopes ?? ["profile.read", "pokemon.read", "pokemon.favorite.write", "pokemon.notes.write"]))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalJwt);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ExchangeCodeAsync(string code, string verifier) =>
        _client.PostAsJsonAsync(
            "/api/integrations/household/v1/token",
            new HouseholdTokenRequest(
                "authorization_code",
                "household",
                RedirectUri,
                code,
                verifier,
                null));

    private async Task<(int PokemonA, int PokemonB)> SeedPokemonAsync(int userA, int userB)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fileA = NewFile(userA, Unique("sha-a"));
        var fileB = NewFile(userB, Unique("sha-b"));
        db.Files.AddRange(fileA, fileB);
        await db.SaveChangesAsync();

        var pokemonA = new PokemonEntity
        {
            UserId = userA,
            FileId = fileA.Id,
            SpeciesId = 25,
            Nickname = "Owned by A",
            OtName = "Sensitive A",
            Tid = 111,
            Level = 20,
            Notes = "private-a"
        };
        var pokemonB = new PokemonEntity
        {
            UserId = userB,
            FileId = fileB.Id,
            SpeciesId = 1,
            Nickname = "Owned by B",
            OtName = "Sensitive B",
            Tid = 222,
            Level = 10,
            Notes = "private-b"
        };
        db.Pokemon.AddRange(pokemonA, pokemonB);
        await db.SaveChangesAsync();
        return (pokemonA.Id, pokemonB.Id);
    }

    private static FileEntity NewFile(int userId, string sha) => new()
    {
        UserId = userId,
        Sha256 = sha,
        FileName = $"{sha}.pk9",
        OriginalFileName = $"{sha}.pk9",
        Format = "pk9",
        StoredPath = $"test/{sha}.pk9",
        Size = 344,
        ImportedAt = DateTime.UtcNow
    };

    private async Task<HttpResponseMessage> SendWithBearerAsync(
        HttpMethod method,
        string uri,
        string token,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string CreateChallenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => Uri.UnescapeDataString(part[0]), part => Uri.UnescapeDataString(part[1]));

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}

public sealed class HouseholdApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        AppContext.BaseDirectory,
        $"beast-vault-household-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath};Pooling=False",
                ["BeastVault:SkipStartupScan"] = "true",
                ["HouseholdIntegration:ClientId"] = "household",
                ["HouseholdIntegration:RedirectUris:0"] = RedirectForTests,
                ["HouseholdIntegration:AccessTokenMinutes"] = "15",
                ["HouseholdIntegration:RefreshTokenDays"] = "30",
                ["HouseholdIntegration:AuthorizationCodeMinutes"] = "5"
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Legacy migrations rely on the application's compatibility patch for these
        // columns. Ensure the isolated fresh database matches that runtime schema.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS PokemonBoxes (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT 'Box',
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS PokemonBoxSlots (
                BoxId INTEGER NOT NULL,
                SlotIndex INTEGER NOT NULL,
                PokemonId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (BoxId, SlotIndex),
                FOREIGN KEY (BoxId) REFERENCES PokemonBoxes(Id) ON DELETE CASCADE,
                FOREIGN KEY (PokemonId) REFERENCES Pokemon(Id) ON DELETE CASCADE);");
        foreach (var sql in new[]
        {
            "ALTER TABLE Tags ADD COLUMN Category INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Tags ADD COLUMN ColorHex TEXT",
            "ALTER TABLE Tags ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Tags ADD COLUMN Description TEXT",
            "ALTER TABLE PokemonTags ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0"
        })
        {
            try { db.Database.ExecuteSqlRaw(sql); }
            catch (Microsoft.Data.Sqlite.SqliteException exception)
                when (exception.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }
        }

        return host;
    }

    private const string RedirectForTests = "http://localhost:5019/integrations/callback/provider";

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            try { File.Delete(_databasePath); }
            catch (IOException) { /* SQLite may release its pooled handle shortly after host disposal. */ }
        }
    }
}
