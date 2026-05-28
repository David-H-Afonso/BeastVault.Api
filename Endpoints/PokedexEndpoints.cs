using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Contracts;
namespace BeastVault.Api.Endpoints;

public static class PokedexEndpoints
{
    public static IEndpointRouteBuilder MapPokedexEndpoints(this IEndpointRouteBuilder app)
    {
        var pokedex = app.MapGroup("/pokedex").WithTags("Pokedex");

        // Public: get cached species data (requires auth but not admin)
        pokedex.MapGet("/species/{speciesId:int}", async (int speciesId, IPokedexService pokedexService) =>
        {
            var result = await pokedexService.GetSpeciesWithFormsAsync(speciesId);
            return Results.Ok(result);
        })
        .WithName("GetPokedexSpecies")
        .WithSummary("Get cached Pokédex species data with all forms")
        .RequireAuthorization();

        // Public: get cached pokemon form data
        pokedex.MapGet("/pokemon/{pokemonId:int}", async (int pokemonId, IPokedexService pokedexService) =>
        {
            var result = await pokedexService.GetPokemonAsync(pokemonId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPokedexPokemon")
        .WithSummary("Get cached Pokédex form data")
        .RequireAuthorization();

        // Public: get all forms for a species
        pokedex.MapGet("/species/{speciesId:int}/forms", async (int speciesId, IPokedexService pokedexService) =>
        {
            var forms = await pokedexService.GetPokemonBySpeciesAsync(speciesId);
            return Results.Ok(forms);
        })
        .WithName("GetPokedexSpeciesForms")
        .WithSummary("Get all cached forms for a species")
        .RequireAuthorization();

        // Public: population status
        pokedex.MapGet("/status", async (IPokedexService pokedexService) =>
        {
            var status = await pokedexService.GetPopulationStatusAsync();
            return Results.Ok(status);
        })
        .WithName("GetPokedexStatus")
        .WithSummary("Get Pokédex cache population status")
        .RequireAuthorization();

        // Admin: populate pokedex cache (fire-and-forget, poll /status for progress)
        pokedex.MapPost("/populate", (PopulateRequest request, IServiceScopeFactory scopeFactory) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 1025;

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range. Max endId is 10000." });

            if (PokedexService.IsPopulating)
                return Results.Conflict(new { message = "Population is already in progress. Check /pokedex/status for progress." });

            // Fire and forget - use a new scope so the DbContext lives for the full duration
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try
                {
                    await service.PopulateSpeciesRangeAsync(startId, endId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background populate error: {ex.Message}");
                }
            });

            return Results.Accepted(value: new { message = $"Population started for species {startId}-{endId}. Poll /pokedex/status for progress." });
        })
        .WithName("PopulatePokedex")
        .WithSummary("Populate Pokédex cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        // Admin: populate items cache
        pokedex.MapPost("/populate-items", (PopulateItemsRequest request, IServiceScopeFactory scopeFactory) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 2180;

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range." });

            if (PokedexService.IsPopulatingItems)
                return Results.Conflict(new { message = "Item population is already in progress. Check /pokedex/status for progress." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try
                {
                    await service.PopulateItemsAsync(startId, endId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background item populate error: {ex.Message}");
                }
            });

            return Results.Accepted(value: new { message = $"Item population started for {startId}-{endId}." });
        })
        .WithName("PopulateItems")
        .WithSummary("Populate item cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        // Admin: populate moves cache
        pokedex.MapPost("/populate-moves", (PopulateMovesRequest request, IServiceScopeFactory scopeFactory) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 919;

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range." });

            if (PokedexService.IsPopulatingMoves)
                return Results.Conflict(new { message = "Move population is already in progress. Check /pokedex/status for progress." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try
                {
                    await service.PopulateMovesAsync(startId, endId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background move populate error: {ex.Message}");
                }
            });

            return Results.Accepted(value: new { message = $"Move population started for {startId}-{endId}." });
        })
        .WithName("PopulateMoves")
        .WithSummary("Populate move cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        // Public: get cached item data
        pokedex.MapGet("/item/{itemId:int}", async (int itemId, IPokedexService pokedexService) =>
        {
            var result = await pokedexService.GetItemAsync(itemId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPokedexItem")
        .WithSummary("Get cached item data")
        .RequireAuthorization();

        // Admin: download sprites to local disk (fire-and-forget, poll /pokedex/sprites-status)
        pokedex.MapPost("/download-sprites", (IServiceScopeFactory scopeFactory) =>
        {
            if (ImageCacheService.IsDownloading)
                return Results.Conflict(new { message = "Sprite download is already in progress. Check /pokedex/sprites-status." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ImageCacheService>();
                try
                {
                    await svc.DownloadAllSpritesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background sprite download error: {ex.Message}");
                }
            });

            return Results.Accepted(value: new { message = "Sprite download started. Poll /pokedex/sprites-status for progress." });
        })
        .WithName("DownloadSprites")
        .WithSummary("Download all Pokémon and item sprites to local disk (admin only)")
        .RequireAuthorization("AdminPolicy");

        // Admin: sprite download status
        pokedex.MapGet("/sprites-status", async (IPokedexService pokedexService) =>
        {
            var status = await pokedexService.GetSpriteDownloadStatusAsync();
            return Results.Ok(status);
        })
        .WithName("GetSpriteDownloadStatus")
        .WithSummary("Get status of local sprite cache")
        .RequireAuthorization();

        // ── Abilities ──────────────────────────────────────────────────────

        pokedex.MapGet("/ability/{abilityId:int}", async (int abilityId, IPokedexService svc) =>
        {
            var result = await svc.GetAbilityAsync(abilityId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPokedexAbility")
        .WithSummary("Get cached ability data")
        .RequireAuthorization();

        pokedex.MapPost("/populate-abilities", (PopulateAbilitiesRequest request, IServiceScopeFactory scopeFactory) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 307;

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range." });

            if (PokedexService.IsPopulatingAbilities)
                return Results.Conflict(new { message = "Ability population is already in progress. Check /pokedex/status for progress." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try { await service.PopulateAbilitiesAsync(startId, endId); }
                catch (Exception ex) { Console.WriteLine($"Background ability populate error: {ex.Message}"); }
            });

            return Results.Accepted(value: new { message = $"Ability population started for {startId}-{endId}." });
        })
        .WithName("PopulateAbilities")
        .WithSummary("Populate ability cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        // ── Types ──────────────────────────────────────────────────────────

        pokedex.MapGet("/type/{typeId:int}", async (int typeId, IPokedexService svc) =>
        {
            var result = await svc.GetTypeAsync(typeId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPokedexType")
        .WithSummary("Get cached type with damage relations")
        .RequireAuthorization();

        pokedex.MapGet("/types", async (IPokedexService svc) =>
        {
            var types = await svc.GetAllTypesAsync();
            return Results.Ok(types);
        })
        .WithName("GetAllPokedexTypes")
        .WithSummary("Get all cached types")
        .RequireAuthorization();

        pokedex.MapPost("/populate-types", (IServiceScopeFactory scopeFactory) =>
        {
            if (PokedexService.IsPopulatingTypes)
                return Results.Conflict(new { message = "Type population is already in progress." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try { await service.PopulateTypesAsync(); }
                catch (Exception ex) { Console.WriteLine($"Background type populate error: {ex.Message}"); }
            });

            return Results.Accepted(value: new { message = "Type population started (18 types)." });
        })
        .WithName("PopulateTypes")
        .WithSummary("Populate type cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        // ── Evolution Chains ──────────────────────────────────────────────

        pokedex.MapGet("/evolution-chain/{chainId:int}", async (int chainId, IPokedexService svc) =>
        {
            var result = await svc.GetEvolutionChainAsync(chainId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetEvolutionChain")
        .WithSummary("Get cached evolution chain JSON")
        .RequireAuthorization();

        pokedex.MapGet("/species/{speciesId:int}/evolution-chain", async (int speciesId, IPokedexService svc) =>
        {
            var result = await svc.GetEvolutionChainBySpeciesAsync(speciesId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetEvolutionChainBySpecies")
        .WithSummary("Get cached evolution chain for a species")
        .RequireAuthorization();

        pokedex.MapPost("/populate-evolution-chains", (PopulateEvolutionChainsRequest request, IServiceScopeFactory scopeFactory) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 549;

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range." });

            if (PokedexService.IsPopulatingChains)
                return Results.Conflict(new { message = "Evolution chain population is already in progress. Check /pokedex/status for progress." });

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPokedexService>();
                try { await service.PopulateEvolutionChainsAsync(startId, endId); }
                catch (Exception ex) { Console.WriteLine($"Background chain populate error: {ex.Message}"); }
            });

            return Results.Accepted(value: new { message = $"Evolution chain population started for {startId}-{endId}." });
        })
        .WithName("PopulateEvolutionChains")
        .WithSummary("Populate evolution chain cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        return app;
    }
}

public record PopulateRequest(int? StartId = null, int? EndId = null);
public record PopulateItemsRequest(int? StartId = null, int? EndId = null);
public record PopulateMovesRequest(int? StartId = null, int? EndId = null);
public record PopulateAbilitiesRequest(int? StartId = null, int? EndId = null);
public record PopulateEvolutionChainsRequest(int? StartId = null, int? EndId = null);
