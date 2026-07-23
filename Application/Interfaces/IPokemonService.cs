using BeastVault.Api.Contracts;

namespace BeastVault.Api.Application.Interfaces;

public interface IPokemonService
{
    Task<PokemonListResponseDto> GetPokemonListAsync(int userId, AdvancedPokemonQuery query);
    Task<PokemonSummaryDto> GetPokemonSummaryAsync(int userId);
    Task<TagFacetCountsDto> GetTagFacetCountsAsync(int userId, AdvancedPokemonQuery query);
    Task<PokemonDetailDto?> GetPokemonByIdAsync(int userId, int pokemonId);
    Task<string?> GetShowdownExportAsync(int userId, int pokemonId);
    Task<bool> UpdatePokemonAsync(int userId, int pokemonId, UpdatePokemonDto dto);
    Task<bool> UpdateFavoriteAsync(int userId, int pokemonId, bool favorite);
    Task<bool> UpdateNotesAsync(int userId, int pokemonId, string? notes);
    Task<object?> ComparePokemonAsync(int userId, int id1, int id2);
    Task<(bool Success, bool FileDeleted, bool BackupPreserved)> DeletePokemonDatabaseAsync(int userId, int pokemonId);
    Task<(bool Success, bool FileDeleted, bool BackupDeleted, string? FileName)> DeletePokemonAndBackupAsync(int userId, int pokemonId);
}
