using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Application.Interfaces;

public interface IPokedexService
{
    Task<PokedexEntry?> GetSpeciesAsync(int speciesId);
    Task<PokedexPokemon?> GetPokemonAsync(int pokemonId);
    Task<PokedexItem?> GetItemAsync(int itemId);
    Task<PokedexItem?> GetOrFetchItemAsync(int itemId);
    Task<List<PokedexPokemon>> GetPokemonBySpeciesAsync(int speciesId);
    Task<SpeciesWithFormsResponse> GetSpeciesWithFormsAsync(int speciesId);
    Task<int> PopulateSpeciesRangeAsync(int startId, int endId, IProgress<string>? progress = null);
    Task<int> PopulateItemsAsync(int startId, int endId);
    Task<PopulationStatusResponse> GetPopulationStatusAsync();
}
