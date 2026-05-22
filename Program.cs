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

// Asegurar que exista la carpeta de almacenamiento y la BD
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Asegurar que la base de datos esté creada con el esquema actual
    try
    {
        // Usar migraciones en lugar de EnsureCreated para aplicar cambios de esquema
        await db.Database.MigrateAsync();
        Console.WriteLine("✅ Base de datos migrada correctamente.");
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
