using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Application.Interfaces;

namespace BeastVault.Api.Application.Services;

public class TagService : ITagService
{
    private readonly AppDbContext _db;

    public TagService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TagDto>> GetTagsAsync(int userId)
    {
        return await _db.Tags
            .Where(t => t.UserId == null || t.UserId == userId)
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                ImagePath = t.ImagePath,
                PokemonCount = t.PokemonTags.Count(pt => pt.Pokemon.UserId == userId)
            })
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<TagDto?> GetTagByIdAsync(int userId, int tagId)
    {
        return await _db.Tags
            .Where(t => t.Id == tagId && (t.UserId == null || t.UserId == userId))
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                ImagePath = t.ImagePath,
                PokemonCount = t.PokemonTags.Count(pt => pt.Pokemon.UserId == userId)
            })
            .FirstOrDefaultAsync();
    }

    public async Task<TagDto?> CreateTagAsync(int userId, string name)
    {
        var existing = await _db.Tags.AnyAsync(t => t.Name == name && t.UserId == userId);
        if (existing) return null;

        var tag = new TagEntity { Name = name, UserId = userId };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();

        return new TagDto { Id = tag.Id, Name = tag.Name, PokemonCount = 0 };
    }

    public async Task<TagDto?> UpdateTagAsync(int userId, int tagId, string name)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
        if (tag == null) return null;

        tag.Name = name;
        await _db.SaveChangesAsync();

        return new TagDto { Id = tag.Id, Name = tag.Name, ImagePath = tag.ImagePath, PokemonCount = 0 };
    }

    public async Task<bool> DeleteTagAsync(int userId, int tagId)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
        if (tag == null) return false;

        var pokemonTags = await _db.PokemonTags.Where(pt => pt.TagId == tagId).ToListAsync();
        _db.PokemonTags.RemoveRange(pokemonTags);
        var fileTags = await _db.FileTags.Where(ft => ft.TagId == tagId).ToListAsync();
        _db.FileTags.RemoveRange(fileTags);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<List<TagDto>> GetPokemonTagsAsync(int userId, int pokemonId)
    {
        var pokemon = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pokemonId && p.UserId == userId);
        if (pokemon == null) return new List<TagDto>();

        return await _db.PokemonTags
            .Where(pt => pt.PokemonId == pokemonId)
            .Select(pt => new TagDto
            {
                Id = pt.Tag.Id,
                Name = pt.Tag.Name,
                ImagePath = pt.Tag.ImagePath,
                PokemonCount = 0
            })
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<List<TagDto>?> SetPokemonTagsAsync(int userId, int pokemonId, int[] tagIds)
    {
        var pokemon = await _db.Pokemon.FirstOrDefaultAsync(p => p.Id == pokemonId && p.UserId == userId);
        if (pokemon == null) return null;

        var existingTags = await _db.PokemonTags.Where(pt => pt.PokemonId == pokemonId).ToListAsync();
        _db.PokemonTags.RemoveRange(existingTags);

        foreach (var tagId in tagIds)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && (t.UserId == null || t.UserId == userId));
            if (tag != null)
            {
                _db.PokemonTags.Add(new PokemonTagEntity { PokemonId = pokemonId, TagId = tagId });
            }
        }

        await _db.SaveChangesAsync();

        return await GetPokemonTagsAsync(userId, pokemonId);
    }

    public async Task<bool> RemovePokemonTagsAsync(int userId, int pokemonId)
    {
        var pokemon = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pokemonId && p.UserId == userId);
        if (pokemon == null) return false;

        var tags = await _db.PokemonTags.Where(pt => pt.PokemonId == pokemonId).ToListAsync();
        _db.PokemonTags.RemoveRange(tags);
        await _db.SaveChangesAsync();

        return true;
    }
}
