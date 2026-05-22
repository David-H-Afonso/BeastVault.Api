using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
using static BeastVault.Api.Endpoints.ConfigurationEndpoints;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks();
app.MapAuthEndpoints();
app.MapSpriteEndpoints();
app.MapImportEndpoints();
app.MapPokemonEndpoints();
app.MapTagEndpoints();
app.MapFilesEndpoints();
app.MapScanEndpoints();
app.MapMaintenanceEndpoints();
app.MapConfigurationEndpoints();
app.MapPokedexEndpoints();

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
    catch (Exception ex) when (ex.Message.Contains("already exists"))
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
            "20260522173522_AddPreferencesAndPokedexCache" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ('{mig}', '9.0.8')";
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("✅ Schema repaired successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al migrar la base de datos: {ex.Message}");
        Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
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

await app.RunAsync();
