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

        // Public: get cached item data
        pokedex.MapGet("/item/{itemId:int}", async (int itemId, IPokedexService pokedexService) =>
        {
            var result = await pokedexService.GetItemAsync(itemId);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetPokedexItem")
        .WithSummary("Get cached item data")
        .RequireAuthorization();

        return app;
    }
}

public record PopulateRequest(int? StartId = null, int? EndId = null);
public record PopulateItemsRequest(int? StartId = null, int? EndId = null);
