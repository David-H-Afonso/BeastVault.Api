using BeastVault.Api.Contracts;

namespace BeastVault.Api.Application.Interfaces;

public interface IPokemonService
{
    Task<object> GetPokemonListAsync(int userId, AdvancedPokemonQuery query);
    Task<PokemonDetailDto?> GetPokemonByIdAsync(int userId, int pokemonId);
    Task<string?> GetShowdownExportAsync(int userId, int pokemonId);
    Task<bool> UpdatePokemonAsync(int userId, int pokemonId, UpdatePokemonDto dto);
    Task<object?> ComparePokemonAsync(int userId, int id1, int id2);
    Task<(bool Success, bool FileDeleted, bool BackupPreserved)> DeletePokemonDatabaseAsync(int userId, int pokemonId);
    Task<(bool Success, bool FileDeleted, bool BackupDeleted, string? FileName)> DeletePokemonAndBackupAsync(int userId, int pokemonId);
}
