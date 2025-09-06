using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Endpoints;
using BeastVault.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Infrastructure.Configuration;
using static BeastVault.Api.Endpoints.ConfigurationEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrar servicio de configuración de almacenamiento
builder.Services.AddSingleton<StorageConfiguration>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        // Para Electron, desarrollo local y CasaOS
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            var uri = new Uri(origin);
            
            // Permitir localhost y 127.0.0.1 (desarrollo y Electron)
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                return true;
            
            // Permitir redes locales (CasaOS y desarrollo)
            if (uri.Host.StartsWith("192.168.") || 
                uri.Host.StartsWith("10.") || 
                uri.Host.StartsWith("172."))
                return true;
                
            return false;
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
app.MapConfigurationEndpoints();

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
            // Usar StorageConfiguration para obtener la ruta de la base de datos
            services.AddDbContext<AppDbContext>((sp, opt) =>
            {
                var storageConfig = sp.GetRequiredService<StorageConfiguration>();

                // Registrar la configuración actual
                storageConfig.LogCurrentConfiguration();

                // Usar la cadena de conexión configurada
                var connectionString = config.GetConnectionString("Default");
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = storageConfig.GetConnectionString();
                }

                opt.UseSqlite(connectionString);
            });

            return services;
        }
        public static IServiceCollection AddBeastVaultServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<FileStorageService>(sp =>
            {
                var storageConfig = sp.GetRequiredService<StorageConfiguration>();
                return new FileStorageService(storageConfig);
            });

            services.AddScoped<BeastVault.Api.Infrastructure.Services.PkhexCoreParser>();
            services.AddScoped<BeastVault.Api.Infrastructure.Services.FileWatcherService>();
            return services;
        }
    }
}
