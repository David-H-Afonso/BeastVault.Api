using BeastVault.Api.Contracts;

namespace BeastVault.Api.Application.Interfaces;

public interface ITagService
{
    Task<List<TagDto>> GetTagsAsync(int userId);
    Task<TagDto?> GetTagByIdAsync(int userId, int tagId);
    Task<TagDto?> CreateTagAsync(int userId, string name);
    Task<TagDto?> UpdateTagAsync(int userId, int tagId, string name);
    Task<bool> DeleteTagAsync(int userId, int tagId);
    Task<List<TagDto>> GetPokemonTagsAsync(int userId, int pokemonId);
    Task<List<TagDto>?> SetPokemonTagsAsync(int userId, int pokemonId, int[] tagIds);
    Task<bool> RemovePokemonTagsAsync(int userId, int pokemonId);
}
