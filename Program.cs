using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BeastVault.Api.Configuration;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Endpoints;
using BeastVault.Api.Extensions;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Infrastructure.Configuration;
using BeastVault.Api.Middleware;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Security;
using static BeastVault.Api.Endpoints.ConfigurationEndpoints;

var builder = WebApplication.CreateBuilder(args);

var householdClientIdEnv = Environment.GetEnvironmentVariable("HOUSEHOLD_CLIENT_ID");
if (!string.IsNullOrWhiteSpace(householdClientIdEnv))
    builder.Configuration["HouseholdIntegration:ClientId"] = householdClientIdEnv;

var householdRedirectUrisEnv = Environment.GetEnvironmentVariable("HOUSEHOLD_REDIRECT_URIS");
if (!string.IsNullOrWhiteSpace(householdRedirectUrisEnv))
{
    var redirectUris = householdRedirectUrisEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var index = 0; index < redirectUris.Length; index++)
        builder.Configuration[$"HouseholdIntegration:RedirectUris:{index}"] = redirectUris[index];
}

foreach (var (environmentName, configurationName) in new[]
{
    ("HOUSEHOLD_ACCESS_TOKEN_MINUTES", "AccessTokenMinutes"),
    ("HOUSEHOLD_REFRESH_TOKEN_DAYS", "RefreshTokenDays"),
    ("HOUSEHOLD_AUTHORIZATION_CODE_MINUTES", "AuthorizationCodeMinutes")
})
{
    var value = Environment.GetEnvironmentVariable(environmentName);
    if (!string.IsNullOrWhiteSpace(value))
        builder.Configuration[$"HouseholdIntegration:{configurationName}"] = value;
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<StorageConfiguration>();

// JWT settings — env var overrides
var jwtSecretEnv = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (!string.IsNullOrWhiteSpace(jwtSecretEnv))
    builder.Configuration["JwtSettings:SecretKey"] = jwtSecretEnv;

var jwtAccessMinEnv = Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_MINUTES");
if (!string.IsNullOrWhiteSpace(jwtAccessMinEnv))
    builder.Configuration["JwtSettings:AccessTokenMinutes"] = jwtAccessMinEnv;

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddOptions<HouseholdIntegrationSettings>()
    .Bind(builder.Configuration.GetSection(HouseholdIntegrationSettings.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ClientId), "Household ClientId is required.")
    .Validate(settings => settings.RedirectUris.Length > 0, "At least one exact Household redirect URI is required.")
    .Validate(settings => settings.RedirectUris.All(uri =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && string.IsNullOrEmpty(parsed.Fragment)),
        "Household redirect URIs must be absolute and cannot contain fragments.")
    .Validate(settings => settings.AccessTokenMinutes is > 0 and <= 60, "Household access-token lifetime is invalid.")
    .Validate(settings => settings.RefreshTokenDays is > 0 and <= 90, "Household refresh-token lifetime is invalid.")
    .Validate(settings => settings.AuthorizationCodeMinutes is > 0 and <= 10, "Household authorization-code lifetime is invalid.")
    .ValidateOnStart();

// JWT authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

if (builder.Environment.IsProduction())
{
    var knownDefaults = new[]
    {
        "BeastVault-Dev-Secret-Key-Change-In-Production-Min32Chars!!"
    };

    if (knownDefaults.Any(d => string.Equals(d, jwtSettings.SecretKey, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "JWT SecretKey is set to a default/insecure value. " +
            "Set a strong, unique key via the JWT_SECRET_KEY environment variable or JwtSettings:SecretKey configuration before running in Production.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
         ClockSkew = TimeSpan.Zero
         };
     })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HouseholdIntegrationAuthenticationHandler>(
        HouseholdIntegrationDefaults.AuthenticationScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    options.AddPolicy("NormalUserOnly", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
    options.AddPolicy("HouseholdIntegrationOnly", policy =>
    {
        policy.AddAuthenticationSchemes(HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
    options.AddPolicy("HouseholdProfileReadPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HouseholdScopeRequirement("profile.read"));
    });
    options.AddPolicy("HouseholdPokemonDownloadPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HouseholdScopeRequirement("pokemon.download"));
    });
    options.AddPolicy("PokemonReadPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HouseholdScopeRequirement("pokemon.read"));
    });
    options.AddPolicy("PokemonFavoriteWritePolicy", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HouseholdScopeRequirement("pokemon.favorite.write"));
    });
    options.AddPolicy("PokemonNotesWritePolicy", policy =>
    {
        policy.AddAuthenticationSchemes(
            JwtBearerDefaults.AuthenticationScheme,
            HouseholdIntegrationDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HouseholdScopeRequirement("pokemon.notes.write"));
    });
});

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, HouseholdScopeAuthorizationHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IHouseholdIntegrationService, HouseholdIntegrationService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("household-authorize", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("household-token", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

// Auth service
builder.Services.AddScoped<IAuthService, AuthService>();

// Pokedex service + HttpClient for PokeAPI
builder.Services.AddScoped<IPokedexService, PokedexService>();
builder.Services.AddHttpClient("PokeApi", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BeastVault/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Bulbapedia service + HttpClient
builder.Services.AddScoped<IBulbapediaService, BulbapediaService>();
builder.Services.AddHttpClient("Bulbapedia", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BeastVault/1.0 (Pokemon collection app)");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// WikiDex service + HttpClient (Spanish partner wiki for Gen 1–9 flavor text)
builder.Services.AddScoped<IWikidexService, WikidexService>();
builder.Services.AddHttpClient("Wikidex", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BeastVault/1.0 (Pokemon collection app)");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// JaWiki service + HttpClient (Japanese partner wiki for Gen 1–9 flavor text)
builder.Services.AddScoped<IJaWikiService, JaWikiService>();
builder.Services.AddHttpClient("JaWiki", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BeastVault/1.0 (Pokemon collection app)");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// CORS — read comma-separated origins from CORS_ALLOWED_ORIGINS env var
var corsOriginsRaw = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
if (!string.IsNullOrWhiteSpace(corsOriginsRaw))
{
    var parsedOrigins = corsOriginsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var i = 0; i < parsedOrigins.Length; i++)
        builder.Configuration[$"CorsSettings:AllowedOrigins:{i}"] = parsedOrigins[i];
}

var corsAllowedOrigins = builder.Configuration
    .GetSection("CorsSettings:AllowedOrigins")
    .Get<List<string>>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        if (corsAllowedOrigins.Count > 0)
        {
            policy.WithOrigins(corsAllowedOrigins.ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
        }
        else
        {
            // Fallback para dev local y Electron: permitir localhost y redes privadas
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                var uri = new Uri(origin);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1"
                    || uri.Host.StartsWith("192.168.")
                    || uri.Host.StartsWith("10.")
                    || uri.Host.StartsWith("172.");
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
        }
    });
});

builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddBeastVaultServices(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors("AllowLocalhost");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks();
app.MapAuthEndpoints();
app.MapHouseholdIntegrationEndpoints();
app.MapSpriteEndpoints();
app.MapImportEndpoints();
app.MapPokemonEndpoints();
app.MapTagEndpoints();
app.MapBoxesEndpoints();
app.MapFilesEndpoints();
app.MapScanEndpoints();
app.MapMaintenanceEndpoints();
app.MapConfigurationEndpoints();
app.MapPokedexEndpoints();
app.MapVaultPokedexEndpoints();

// Asegurar que exista la carpeta de almacenamiento y la BD
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Asegurar que la base de datos esté creada con el esquema actual
    try
    {
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ Base de datos migrada correctamente.");
    }
    catch (Exception ex) when (
        ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("duplicate table", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
    {
        // Pre-existing database without migration history (like Games Database pattern).
        // Tables exist but EF can't apply migrations. Repair schema manually.
        Console.WriteLine($"⚠️ Migration failed: {ex.Message}");
        Console.WriteLine("⚠️ Pre-existing database detected. Repairing schema...");

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        // 1. Create Users table
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Users"" PRIMARY KEY AUTOINCREMENT,
                ""Username"" TEXT NOT NULL,
                ""PasswordHash"" TEXT NULL,
                ""Role"" INTEGER NOT NULL DEFAULT 0,
                ""IsDefault"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'
            )";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("  ✅ Users table ensured");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Username"" ON ""Users"" (""Username"")";
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Add UserId columns if missing
        foreach (var (table, nullable) in new[] { ("Files", false), ("Pokemon", false), ("Tags", true) })
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='UserId'";
            var colExists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) > 0;

            if (!colExists)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = nullable
                    ? $@"ALTER TABLE ""{table}"" ADD COLUMN ""UserId"" INTEGER"
                    : $@"ALTER TABLE ""{table}"" ADD COLUMN ""UserId"" INTEGER NOT NULL DEFAULT 1";
                await alterCmd.ExecuteNonQueryAsync();

                if (nullable)
                {
                    using var updateCmd = conn.CreateCommand();
                    updateCmd.CommandText = $@"UPDATE ""{table}"" SET ""UserId"" = 1 WHERE ""UserId"" IS NULL";
                    await updateCmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"  ✅ Added UserId to {table}");
            }
        }

        // 3. Create indexes (IF NOT EXISTS is idempotent)
        var indexes = new[]
        {
            @"CREATE INDEX IF NOT EXISTS ""IX_Tags_UserId"" ON ""Tags"" (""UserId"")",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Tags_UserId_Name"" ON ""Tags"" (""UserId"", ""Name"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_Pokemon_UserId"" ON ""Pokemon"" (""UserId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_Files_UserId"" ON ""Files"" (""UserId"")",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Files_UserId_Sha256"" ON ""Files"" (""UserId"", ""Sha256"")",
        };
        foreach (var sql in indexes)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Drop old unique indexes (replaced by composite ones above)
        foreach (var idx in new[] { "IX_Tags_Name", "IX_Files_Sha256" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"DROP INDEX IF EXISTS ""{idx}""";
            try { await cmd.ExecuteNonQueryAsync(); } catch { /* may not exist */ }
        }

        // 5. Create new tables from AddPreferencesAndPokedexCache migration
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""UserPreferences"" (
                ""UserId"" INTEGER NOT NULL CONSTRAINT ""PK_UserPreferences"" PRIMARY KEY,
                ""Theme"" TEXT NOT NULL DEFAULT 'dark',
                ""ViewMode"" TEXT NOT NULL DEFAULT 'grid',
                ""SpriteType"" TEXT NOT NULL DEFAULT 'sprites',
                ""BackgroundType"" TEXT NOT NULL DEFAULT 'diagonal-45',
                CONSTRAINT ""FK_UserPreferences_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
            )";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("  ✅ UserPreferences table ensured");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""PokedexEntries"" (
                ""SpeciesId"" INTEGER NOT NULL CONSTRAINT ""PK_PokedexEntries"" PRIMARY KEY,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""LocalizedNames"" TEXT NOT NULL DEFAULT '{}',
                ""Genus"" TEXT NOT NULL DEFAULT '',
                ""FlavorText"" TEXT NOT NULL DEFAULT '',
                ""Generation"" INTEGER NOT NULL DEFAULT 0,
                ""Color"" TEXT NOT NULL DEFAULT '',
                ""Shape"" TEXT NOT NULL DEFAULT '',
                ""Habitat"" TEXT NOT NULL DEFAULT '',
                ""GrowthRate"" TEXT NOT NULL DEFAULT '',
                ""CaptureRate"" INTEGER NOT NULL DEFAULT 0,
                ""BaseHappiness"" INTEGER NOT NULL DEFAULT 0,
                ""HatchCounter"" INTEGER NOT NULL DEFAULT 0,
                ""GenderRate"" INTEGER NOT NULL DEFAULT 0,
                ""IsLegendary"" INTEGER NOT NULL DEFAULT 0,
                ""IsMythical"" INTEGER NOT NULL DEFAULT 0,
                ""IsBaby"" INTEGER NOT NULL DEFAULT 0,
                ""HasGenderDifferences"" INTEGER NOT NULL DEFAULT 0,
                ""FormsSwitchable"" INTEGER NOT NULL DEFAULT 0,
                ""EggGroups"" TEXT NOT NULL DEFAULT '[]',
                ""Varieties"" TEXT NOT NULL DEFAULT '[]',
                ""EvolutionChainUrl"" TEXT NOT NULL DEFAULT '',
                ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'
            )";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("  ✅ PokedexEntries table ensured");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""PokedexPokemon"" (
                ""PokemonId"" INTEGER NOT NULL CONSTRAINT ""PK_PokedexPokemon"" PRIMARY KEY,
                ""SpeciesId"" INTEGER NOT NULL DEFAULT 0,
                ""Name"" TEXT NOT NULL DEFAULT '',
                ""Height"" INTEGER NOT NULL DEFAULT 0,
                ""Weight"" INTEGER NOT NULL DEFAULT 0,
                ""BaseExperience"" INTEGER NOT NULL DEFAULT 0,
                ""Order"" INTEGER NOT NULL DEFAULT 0,
                ""IsDefault"" INTEGER NOT NULL DEFAULT 0,
                ""Types"" TEXT NOT NULL DEFAULT '[]',
                ""Abilities"" TEXT NOT NULL DEFAULT '[]',
                ""BaseStats"" TEXT NOT NULL DEFAULT '{}',
                ""Sprites"" TEXT NOT NULL DEFAULT '{}',
                ""Cries"" TEXT NOT NULL DEFAULT '{}',
                ""GameIndices"" TEXT NOT NULL DEFAULT '[]',
                ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'
            )";
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE INDEX IF NOT EXISTS ""IX_PokedexPokemon_SpeciesId"" ON ""PokedexPokemon"" (""SpeciesId"")";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("  ✅ PokedexPokemon table ensured");
        }

        // 6. Mark ALL migrations as applied so future MigrateAsync() skips them
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                ""ProductVersion"" TEXT NOT NULL)";
            await cmd.ExecuteNonQueryAsync();
        }
        foreach (var mig in new[] {
            "20250910204519_InitialCreate",
            "20250912122823_EnsureTagsTableExists",
            "20260521134147_AddUserAndMultiUserSupport",
            "20260522173522_AddPreferencesAndPokedexCache",
            "20260522204305_AddPokedexItems",
            "20260522233512_AddPokedexMoves",
            "20260527111546_AddSpriteCacheLocalPaths",
            "20260527160000_AddSpriteDataBlobs",
            "20260527162000_AddHomeSpriteColumns",
            "20260531140436_SyncSchemaWithMetLevel",
            "20260531202009_BulbapediaNormalization",
            "20260601174120_AddPokemonBoxes" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ('{mig}', '9.0.8')";
            await cmd.ExecuteNonQueryAsync();
        }

        // Apply migrations introduced after the repaired baseline during this same
        // startup, so integrations do not observe a partially repaired schema.
        await db.Database.MigrateAsync();

        Console.WriteLine("✅ Schema repaired successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al migrar la base de datos: {ex.Message}");
        Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
    }

    // ── Always-runs column patch ───────────────────────────────────────────────
    // Ensures columns that may be missing due to pre-migration databases are
    // present regardless of which path the startup migration took.
    try
    {
        var patchConn = db.Database.GetDbConnection();
        if (patchConn.State != System.Data.ConnectionState.Open)
            await patchConn.OpenAsync();

        // Enable WAL mode for better concurrent read/write performance
        using var walCmd = patchConn.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=-32000;";
        await walCmd.ExecuteNonQueryAsync();

        async Task EnsureColumnAsync(string table, string column, string definition)
        {
            using var tableCheck = patchConn.CreateCommand();
            tableCheck.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            var tableExists = Convert.ToInt64(await tableCheck.ExecuteScalarAsync()) > 0;
            if (!tableExists)
            {
                Console.WriteLine($"  ⚠️ Column patch skipped: table {table} does not exist");
                return;
            }

            using var check = patchConn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
            var exists = Convert.ToInt64(await check.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                using var alter = patchConn.CreateCommand();
                alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
                await alter.ExecuteNonQueryAsync();
                Console.WriteLine($"  ✅ Column patch: {table}.{column} added");
            }
        }

        async Task EnsureTableAsync(string table, string columnsDdl)
        {
            using var check = patchConn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            var exists = Convert.ToInt64(await check.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                using var create = patchConn.CreateCommand();
                var ddl = columnsDdl.Trim();
                create.CommandText = ddl.EndsWith(")")
                    ? $"CREATE TABLE \"{table}\" ({ddl}"
                    : $"CREATE TABLE \"{table}\" ({ddl})";
                await create.ExecuteNonQueryAsync();
                Console.WriteLine($"  ✅ Table patch: {table} created");
            }
        }

        async Task ExecutePatchSqlAsync(string sql)
        {
            using var cmd = patchConn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        await EnsureColumnAsync("Users", "CreatedAt", "TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");

        // Core vault tables. Some installations predate EF migrations and can have
        // migration history repaired while still missing current model columns.
        await EnsureColumnAsync("Files", "UserId", "INTEGER NOT NULL DEFAULT 1");
        await EnsureColumnAsync("Files", "OriginalFileName", "TEXT");
        await EnsureColumnAsync("Files", "RawBlob", "BLOB");

        await EnsureColumnAsync("Pokemon", "UserId", "INTEGER NOT NULL DEFAULT 1");
        await EnsureColumnAsync("Pokemon", "SpeciesId", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Nickname", "TEXT");
        await EnsureColumnAsync("Pokemon", "OtName", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Pokemon", "Tid", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Sid", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Level", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "IsShiny", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Nature", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "AbilityId", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "BallId", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "TeraType", "INTEGER");
        await EnsureColumnAsync("Pokemon", "HeldItemId", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OriginGame", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Language", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Pokemon", "MetDate", "TEXT");
        await EnsureColumnAsync("Pokemon", "MetLocation", "TEXT");
        await EnsureColumnAsync("Pokemon", "MetLevel", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "SpriteKey", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Pokemon", "Favorite", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Notes", "TEXT");
        await EnsureColumnAsync("Pokemon", "Gender", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OTGender", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OTLanguage", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Pokemon", "EncryptionConstant", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "PersonalityId", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Experience", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "CurrentFriendship", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Form", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "FormArgument", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "DynamaxLevel", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "CanGigantamax", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "IsEgg", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "FatefulEncounter", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "EggLocation", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "EggMetDate", "TEXT");
        await EnsureColumnAsync("Pokemon", "HeightScalar", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "WeightScalar", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "Scale", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "PokerusState", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "PokerusDays", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "PokerusStrain", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestCool", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestBeauty", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestCute", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestSmart", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestTough", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "ContestSheen", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "CurrentHandler", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerName", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerGender", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerLanguage", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerFriendship", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OriginalTrainerMemory", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OriginalTrainerMemoryIntensity", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OriginalTrainerMemoryFeeling", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "OriginalTrainerMemoryVariable", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerMemory", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerMemoryIntensity", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerMemoryFeeling", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Pokemon", "HandlingTrainerMemoryVariable", "INTEGER NOT NULL DEFAULT 0");

        await EnsureColumnAsync("PokedexPokemon", "SpriteLocalPath", "TEXT");
        await EnsureColumnAsync("PokedexPokemon", "ArtworkLocalPath", "TEXT");
        await EnsureColumnAsync("PokedexPokemon", "MovesJson", "TEXT NOT NULL DEFAULT '[]'");
        await EnsureColumnAsync("PokedexItems", "SpriteLocalPath", "TEXT");
        await EnsureColumnAsync("PokedexEntries", "EvolutionChainId", "INTEGER");

        // Backfill EvolutionChainId from EvolutionChainUrl for entries populated before the column existed
        using (var cmd = patchConn.CreateCommand())
        {
            cmd.CommandText = @"
                UPDATE ""PokedexEntries""
                SET ""EvolutionChainId"" = CAST(
                    SUBSTR(
                        RTRIM(""EvolutionChainUrl"", '/'),
                        INSTR(RTRIM(""EvolutionChainUrl"", '/'), '/api/v2/evolution-chain/') + LENGTH('/api/v2/evolution-chain/')
                    ) AS INTEGER)
                WHERE ""EvolutionChainId"" IS NULL
                  AND ""EvolutionChainUrl"" != ''
                  AND ""EvolutionChainUrl"" LIKE '%/evolution-chain/%'";
            var updated = await cmd.ExecuteNonQueryAsync();
            if (updated > 0)
                Console.WriteLine($"  ✅ Backfilled EvolutionChainId for {updated} species entries");
        }

        // Sprite blob columns for PokedexPokemon
        await EnsureColumnAsync("PokedexPokemon", "SpriteData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "ArtworkData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "ArtworkShinyData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "ShinyData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "HomeSpriteData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "HomeShinyData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "ShowdownData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "ShowdownShinyData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "GithubSpriteData", "BLOB");
        await EnsureColumnAsync("PokedexPokemon", "GithubShinySpriteData", "BLOB");

        // New tables — created if they don't exist yet (EF migration may not have run on older DBs)
        await EnsureTableAsync("PokedexAbilities", @"
            ""AbilityId"" INTEGER NOT NULL,
            ""Name"" TEXT NOT NULL DEFAULT '',
            ""DisplayName"" TEXT NOT NULL DEFAULT '',
            ""Effect"" TEXT NOT NULL DEFAULT '',
            ""ShortEffect"" TEXT NOT NULL DEFAULT '',
            ""FlavorText"" TEXT NOT NULL DEFAULT '',
            ""Generation"" INTEGER NOT NULL DEFAULT 0,
            ""IsMainSeries"" INTEGER NOT NULL DEFAULT 0,
            ""CachedAt"" TEXT NOT NULL,
            CONSTRAINT ""PK_PokedexAbilities"" PRIMARY KEY (""AbilityId"")");

        await EnsureTableAsync("PokedexEvolutionChains", @"
            ""ChainId"" INTEGER NOT NULL,
            ""ChainJson"" TEXT NOT NULL DEFAULT '{}',
            ""CachedAt"" TEXT NOT NULL,
            CONSTRAINT ""PK_PokedexEvolutionChains"" PRIMARY KEY (""ChainId"")");

        await EnsureTableAsync("PokedexTypes", @"
            ""TypeId"" INTEGER NOT NULL,
            ""Name"" TEXT NOT NULL DEFAULT '',
            ""DamageRelations"" TEXT NOT NULL DEFAULT '{}',
            ""Generation"" INTEGER NOT NULL DEFAULT 0,
            ""CachedAt"" TEXT NOT NULL,
            CONSTRAINT ""PK_PokedexTypes"" PRIMARY KEY (""TypeId"")");

        // Tag metadata columns (Phase 3 overhaul)
        await EnsureColumnAsync("Tags", "Category", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Tags", "ColorHex", "TEXT");
        await EnsureColumnAsync("Tags", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Tags", "Description", "TEXT");

        // PokemonTag sort order
        await EnsureColumnAsync("PokemonTags", "SortOrder", "INTEGER NOT NULL DEFAULT 0");

        // UserPreference new columns
        await EnsureColumnAsync("UserPreferences", "BrowseLayout", "TEXT NOT NULL DEFAULT 'list'");
        await EnsureColumnAsync("UserPreferences", "OrganizeDensity", "TEXT NOT NULL DEFAULT 'expanded'");
        await EnsureColumnAsync("UserPreferences", "KanbanDragMode", "TEXT NOT NULL DEFAULT 'move'");

        await EnsureTableAsync("PokemonBoxes", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""UserId"" INTEGER NOT NULL,
            ""Name"" TEXT NOT NULL DEFAULT 'Box',
            ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
            ""CreatedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z',
            ""UpdatedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z',
            CONSTRAINT ""FK_PokemonBoxes_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE)");

        await EnsureTableAsync("PokemonBoxSlots", @"
            ""BoxId"" INTEGER NOT NULL,
            ""SlotIndex"" INTEGER NOT NULL,
            ""PokemonId"" INTEGER NOT NULL,
            ""CreatedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z',
            ""UpdatedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z',
            CONSTRAINT ""PK_PokemonBoxSlots"" PRIMARY KEY (""BoxId"", ""SlotIndex""),
            CONSTRAINT ""FK_PokemonBoxSlots_PokemonBoxes_BoxId"" FOREIGN KEY (""BoxId"") REFERENCES ""PokemonBoxes"" (""Id"") ON DELETE CASCADE,
            CONSTRAINT ""FK_PokemonBoxSlots_Pokemon_PokemonId"" FOREIGN KEY (""PokemonId"") REFERENCES ""Pokemon"" (""Id"") ON DELETE CASCADE)");

        await ExecutePatchSqlAsync(@"CREATE INDEX IF NOT EXISTS ""IX_PokemonBoxes_UserId_SortOrder"" ON ""PokemonBoxes"" (""UserId"", ""SortOrder"")");
        await ExecutePatchSqlAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PokemonBoxSlots_PokemonId"" ON ""PokemonBoxSlots"" (""PokemonId"")");

        // Bulbapedia cache table
        await EnsureTableAsync("BulbapediaCache", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""SpeciesId"" INTEGER NOT NULL DEFAULT 0,
            ""PageTitle"" TEXT NOT NULL DEFAULT '',
            ""PageUrl"" TEXT NOT NULL DEFAULT '',
            ""RevisionId"" INTEGER,
            ""PageId"" INTEGER,
            ""RawContent"" TEXT,
            ""ParsedSections"" TEXT,
            ""Status"" INTEGER NOT NULL DEFAULT 0,
            ""ErrorMessage"" TEXT,
            ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");
        await EnsureColumnAsync("BulbapediaCache", "RawHtml", "TEXT");
        await EnsureColumnAsync("BulbapediaCache", "NameMeaning", "TEXT");
        await EnsureColumnAsync("BulbapediaCache", "NormalizedAt", "TEXT");
        await EnsureColumnAsync("BulbapediaCache", "NormalizedStatus", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("BulbapediaCache", "NormalizedError", "TEXT");
        await EnsureColumnAsync("BulbapediaCache", "EntriesCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("BulbapediaCache", "LocationsCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("BulbapediaCache", "SpritesCount", "INTEGER NOT NULL DEFAULT 0");

        // Pokédex flavor entries (multi-language, multi-game)
        await EnsureTableAsync("PokedexFlavorEntries", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""SpeciesId"" INTEGER NOT NULL DEFAULT 0,
            ""Language"" TEXT NOT NULL DEFAULT '',
            ""GameVersion"" TEXT NOT NULL DEFAULT '',
            ""Text"" TEXT NOT NULL DEFAULT '',
            ""Source"" INTEGER NOT NULL DEFAULT 0,
            ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");

        // Pokédex locations
        await EnsureTableAsync("PokedexLocations", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""SpeciesId"" INTEGER NOT NULL DEFAULT 0,
            ""Game"" TEXT NOT NULL DEFAULT '',
            ""Location"" TEXT NOT NULL DEFAULT '',
            ""Method"" TEXT,
            ""Source"" INTEGER NOT NULL DEFAULT 1,
            ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");

        // Pokédex sprite provenance entries
        await EnsureTableAsync("PokedexSpriteEntries", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""SpeciesId"" INTEGER NOT NULL DEFAULT 0,
            ""PokemonId"" INTEGER,
            ""Generation"" INTEGER NOT NULL DEFAULT 0,
            ""GameSlug"" TEXT NOT NULL DEFAULT '',
            ""DisplayLabel"" TEXT NOT NULL DEFAULT '',
            ""NormalLocalPath"" TEXT,
            ""ShinyLocalPath"" TEXT,
            ""BackLocalPath"" TEXT,
            ""BackShinyLocalPath"" TEXT,
            ""SourceUrl"" TEXT,
            ""Source"" INTEGER NOT NULL DEFAULT 1,
            ""SortOrder"" INTEGER NOT NULL DEFAULT 0,
            ""CachedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");
        foreach (var sql in new[]
        {
            @"CREATE INDEX IF NOT EXISTS ""IX_PokedexSpriteEntries_PokemonId"" ON ""PokedexSpriteEntries"" (""PokemonId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_PokedexSpriteEntries_SpeciesId_GameSlug"" ON ""PokedexSpriteEntries"" (""SpeciesId"", ""GameSlug"")"
        })
        {
            using var cmd = patchConn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // Cached images
        await EnsureTableAsync("CachedImages", @"
            ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ""SourceUrl"" TEXT NOT NULL DEFAULT '',
            ""LocalPath"" TEXT NOT NULL DEFAULT '',
            ""ImageType"" TEXT NOT NULL DEFAULT '',
            ""SpeciesId"" INTEGER,
            ""PokemonId"" INTEGER,
            ""DownloadedAt"" TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Column patch error (non-fatal): {ex.Message}");
    }

    // Seed default admin user
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = "Admin",
            PasswordHash = null,
            Role = UserRole.Admin,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Default admin user created (passwordless login).");
    }

    var skipStartupScan = app.Configuration.GetValue<bool>("BeastVault:SkipStartupScan");
    if (!skipStartupScan)
    {
        var storage = scope.ServiceProvider.GetRequiredService<FileStorageService>();
        storage.EnsureVault();

        // Automatically scan for new files on startup
        var fileWatcher = scope.ServiceProvider.GetRequiredService<FileWatcherService>();
        var scanResult = await fileWatcher.ScanAndImportNewFilesAsync();
        if (scanResult.NewlyImported.Any())
        {
            Console.WriteLine($"Startup scan: Imported {scanResult.NewlyImported.Count} new Pokemon files");
        }
    }
}

await app.RunAsync();

// Enable WebApplicationFactory<Program> for integration tests
public partial class Program { }
