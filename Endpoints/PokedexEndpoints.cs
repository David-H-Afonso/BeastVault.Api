using BeastVault.Api.Application.Interfaces;
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

        // Admin: populate pokedex cache
        pokedex.MapPost("/populate", async (PopulateRequest request, IPokedexService pokedexService) =>
        {
            var startId = request.StartId ?? 1;
            var endId = request.EndId ?? 1025; // Current max species in PokeAPI

            if (startId < 1 || endId < startId || endId > 10000)
                return Results.BadRequest(new { message = "Invalid range. Max endId is 10000." });

            var count = await pokedexService.PopulateSpeciesRangeAsync(startId, endId);
            return Results.Ok(new PopulateResponse(
                $"Populated {count} species from {startId} to {endId}",
                count, startId, endId
            ));
        })
        .WithName("PopulatePokedex")
        .WithSummary("Populate Pokédex cache from PokeAPI (admin only)")
        .RequireAuthorization("AdminPolicy");

        return app;
    }
}

public record PopulateRequest(int? StartId = null, int? EndId = null);
