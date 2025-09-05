using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Endpoints;
using BeastVault.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BeastVault.Api.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        // Para Electron y desarrollo local, permitir cualquier localhost
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            var uri = new Uri(origin);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
    });
});

builder.Services.AddAppDbContext(builder.Configuration);
builder.Services.AddBeastVaultServices(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Use CORS before other middleware
app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();


app.MapHealthChecks();
app.MapImportEndpoints();
app.MapPokemonEndpoints();
app.MapFilesEndpoints();
app.MapScanEndpoints();
app.MapMaintenanceEndpoints();

// Asegurar que exista la carpeta de almacenamiento y la BD
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Asegurar que la base de datos esté creada con el esquema actual
    try
    {
        // Este método es más seguro que MigrateAsync, ya que solo crea la base de datos
        // si no existe, pero no intenta aplicar migraciones adicionales
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("Base de datos verificada correctamente.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al verificar la base de datos: {ex.Message}");
    }

    var storage = scope.ServiceProvider.GetRequiredService<BeastVault.Api.Infrastructure.Services.FileStorageService>();
    storage.EnsureVault();

    // Automatically scan for new files on startup
    var fileWatcher = scope.ServiceProvider.GetRequiredService<BeastVault.Api.Infrastructure.Services.FileWatcherService>();
    var scanResult = await fileWatcher.ScanAndImportNewFilesAsync();
    if (scanResult.NewlyImported.Any())
    {
        Console.WriteLine($"Startup scan: Imported {scanResult.NewlyImported.Count} new Pokemon files");
    }
}

await app.RunAsync();

namespace BeastVault.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration config)
        {
            string dbPath = EnvironmentUtils.GetDatabasePath();

            // Si estamos en Docker, notificar la ruta usada
            if (EnvironmentUtils.IsRunningInDocker())
            {
                Console.WriteLine($"Running in Docker, using database path: {dbPath}");
            }

            var configuredCs = config.GetConnectionString("Default");

            string connectionString;
            if (string.IsNullOrEmpty(configuredCs))
            {
                // Ensure the directory exists
                var dbDirectory = Path.GetDirectoryName(dbPath);
                if (!Directory.Exists(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory!);
                    Console.WriteLine($"Created database directory: {dbDirectory}");
                }
                connectionString = $"Data Source={dbPath}";
            }
            else
            {
                connectionString = configuredCs;
            }

            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseSqlite(connectionString);
            });
            return services;
        }
        public static IServiceCollection AddBeastVaultServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<FileStorageService>(sp =>
            {
                string basePath = EnvironmentUtils.GetPokemonFilesPath();

                // Si estamos en Docker, notificar la ruta usada
                if (EnvironmentUtils.IsRunningInDocker())
                {
                    Console.WriteLine($"Running in Docker, using Pokemon files path: {basePath}");
                }

                // Ensure the base directory exists
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    Console.WriteLine($"Created BeastVault directory: {basePath}");
                }

                return new FileStorageService(basePath);
            });
            services.AddScoped<BeastVault.Api.Infrastructure.Services.PkhexCoreParser>();
            services.AddScoped<BeastVault.Api.Infrastructure.Services.FileWatcherService>();
            return services;
        }
    }
}
