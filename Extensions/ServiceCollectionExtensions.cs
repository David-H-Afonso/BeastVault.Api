using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Infrastructure.Configuration;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;

namespace BeastVault.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>((sp, opt) =>
        {
            var storageConfig = sp.GetRequiredService<StorageConfiguration>();

            storageConfig.LogCurrentConfiguration();

            var connectionString = config.GetConnectionString("Default");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = storageConfig.GetConnectionString();
            }

            opt.UseSqlite(connectionString, sqliteOpts =>
            {
                // Allow longer timeouts for background populate operations
                sqliteOpts.CommandTimeout(120);
            });
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

        services.AddScoped<PkhexCoreParser>();
        services.AddScoped<FileWatcherService>();
        services.AddScoped<IPokemonService, PokemonService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ImageCacheService>();
        return services;
    }
}
