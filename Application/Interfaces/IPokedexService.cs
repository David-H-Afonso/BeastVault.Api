using BeastVault.Api.Application.Services;
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
    Task<int> PopulateMovesAsync(int startId, int endId);
    Task<int> PopulateAbilitiesAsync(int startId, int endId);
    Task<int> PopulateTypesAsync();
    Task<int> PopulateEvolutionChainsAsync(int startId, int endId);
    Task<PopulationStatusResponse> GetPopulationStatusAsync();
    Task<SpriteDownloadStatusResponse> GetSpriteDownloadStatusAsync();
    Task<PokedexAbility?> GetAbilityAsync(int abilityId);
    Task<PokedexType?> GetTypeAsync(int typeId);
    Task<List<PokedexType>> GetAllTypesAsync();
    Task<PokedexEvolutionChain?> GetEvolutionChainAsync(int chainId);
    Task<PokedexEvolutionChain?> GetEvolutionChainBySpeciesAsync(int speciesId);
}
