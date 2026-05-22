using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Application.Interfaces;

public interface IPokedexService
{
    Task<PokedexEntry?> GetSpeciesAsync(int speciesId);
    Task<PokedexPokemon?> GetPokemonAsync(int pokemonId);
    Task<List<PokedexPokemon>> GetPokemonBySpeciesAsync(int speciesId);
    Task<SpeciesWithFormsResponse> GetSpeciesWithFormsAsync(int speciesId);
    Task<int> PopulateSpeciesRangeAsync(int startId, int endId, IProgress<string>? progress = null);
    Task<PopulationStatusResponse> GetPopulationStatusAsync();
}
