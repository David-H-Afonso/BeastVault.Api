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
app.MapTagEndpoints();
app.MapFilesEndpoints();
app.MapScanEndpoints();
app.MapMaintenanceEndpoints();
app.MapConfigurationEndpoints();

// Endpoint para buscar sprites custom por patrón (retorna la URL del primero encontrado)
app.MapGet("/custom-sprites/search/{pattern}", (string pattern) =>
{
    // Try multiple possible locations for assets folder
    var possiblePaths = new List<string>
    {
        Path.Combine(Directory.GetCurrentDirectory(), "assets"),
        Path.Combine(AppContext.BaseDirectory, "assets"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
    };
    
    // Check environment variable for custom assets path
    var envAssetsPath = Environment.GetEnvironmentVariable("BEASTVAULT_ASSETS_PATH");
    if (!string.IsNullOrEmpty(envAssetsPath))
    {
        possiblePaths.Insert(0, envAssetsPath);
    }
    
    // For Electron: try parent directory (resources/backend/assets)
    var parentDir = Directory.GetParent(AppContext.BaseDirectory)?.FullName;
    if (parentDir != null)
    {
        possiblePaths.Add(Path.Combine(parentDir, "assets"));
    }
    
    possiblePaths = possiblePaths.Distinct().ToList();
    
    string? assetsPath = null;
    foreach (var path in possiblePaths)
    {
        if (Directory.Exists(path))
        {
            assetsPath = path;
            break;
        }
    }
    
    if (assetsPath == null)
    {
        return Results.NotFound();
    }

    try
    {
        var cleanPattern = Path.GetFileName(pattern); // Security: remove path traversal
        var matchingFiles = Directory.GetFiles(assetsPath, cleanPattern + "*");
        
        if (matchingFiles.Length > 0)
        {
            // Return just the filename
            var filename = Path.GetFileName(matchingFiles[0]);
            return Results.Json(new { fileName = filename, url = $"/custom-sprites/{filename}" });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error searching for sprite pattern '{pattern}': {ex.Message}");
    }

    return Results.NotFound();
})
.WithName("SearchCustomSprite")
.WithTags("Files")
.Produces(200)
.Produces(404);

// Servir sprites custom desde la carpeta assets
app.MapGet("/custom-sprites/{fileName}", (string fileName) =>
{
    // Try multiple possible locations for assets folder
    var possiblePaths = new List<string>
    {
        Path.Combine(Directory.GetCurrentDirectory(), "assets"),
        Path.Combine(AppContext.BaseDirectory, "assets"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
    };
    
    // Check environment variable for custom assets path
    var envAssetsPath = Environment.GetEnvironmentVariable("BEASTVAULT_ASSETS_PATH");
    if (!string.IsNullOrEmpty(envAssetsPath))
    {
        possiblePaths.Insert(0, envAssetsPath);
    }
    
    // For Electron: try parent directory (resources/backend/assets)
    var parentDir = Directory.GetParent(AppContext.BaseDirectory)?.FullName;
    if (parentDir != null)
    {
        possiblePaths.Add(Path.Combine(parentDir, "assets"));
    }
    
    possiblePaths = possiblePaths.Distinct().ToList();
    
    string? assetsPath = null;
    foreach (var path in possiblePaths)
    {
        if (Directory.Exists(path))
        {
            assetsPath = path;
            break;
        }
    }
    
    // Si la carpeta no existe en ninguna ubicación, retornar 404
    if (assetsPath == null)
    {
        Console.WriteLine($"❌ Assets folder not found. Tried:");
        foreach (var path in possiblePaths)
        {
            Console.WriteLine($"   - {path}");
        }
        return Results.NotFound();
    }
    
    Console.WriteLine($"📂 Using assets path: {assetsPath}");
    
    // Primero intentar encontrar el archivo exacto
    var filePath = Path.GetFullPath(Path.Combine(assetsPath, fileName));
    
    // Validate that the resolved path is still within the assets directory
    if (!filePath.StartsWith(Path.GetFullPath(assetsPath) + Path.DirectorySeparatorChar) &&
        !filePath.Equals(Path.GetFullPath(assetsPath)))
    {
        Console.WriteLine($"❌ Security violation: {filePath} is outside assets directory");
        return Results.BadRequest("Invalid file path");
    }

    if (File.Exists(filePath))
    {
        Console.WriteLine($"✅ Found exact file: {filePath}");
        var contentType = fileName.EndsWith(".png") ? "image/png" :
                          fileName.EndsWith(".webp") ? "image/webp" :
                          "application/octet-stream";

        return Results.File(filePath, contentType);
    }

    // Si no existe el archivo exacto, intentar encontrar un archivo que coincida con el patrón
    // Esto es útil cuando los nombres tienen timestamps variables
    try
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        
        Console.WriteLine($"🔍 Searching for pattern: {fileNameWithoutExtension}*{extension}");
        
        // Buscar archivos que comiencen con el mismo nombre (ignorando timestamps)
        var matchingFiles = Directory.GetFiles(assetsPath, fileNameWithoutExtension + "*" + extension);
        
        Console.WriteLine($"📁 Found {matchingFiles.Length} matching files");
        
        if (matchingFiles.Length > 0)
        {
            // Usar el primer archivo coincidente (idealmente el más reciente)
            var matchedFile = matchingFiles[0];
            Console.WriteLine($"✅ Using matched file: {matchedFile}");
            
            var contentType = fileName.EndsWith(".png") ? "image/png" :
                              fileName.EndsWith(".webp") ? "image/webp" :
                              "application/octet-stream";
            
            return Results.File(matchedFile, contentType);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error searching for file pattern: {ex.Message}");
    }

    Console.WriteLine($"❌ File not found: {fileName}");
    return Results.NotFound();
})
.WithName("GetCustomSprite")
.WithTags("Files")
.Produces(200, contentType: "image/png")
.Produces(200, contentType: "image/webp")
.Produces(200, contentType: "application/octet-stream")
.Produces(400)
.Produces(404);

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
