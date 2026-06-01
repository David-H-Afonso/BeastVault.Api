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
                PokemonCount = t.PokemonTags.Count(pt => pt.Pokemon.UserId == userId),
                Category = t.Category.ToString(),
                ColorHex = t.ColorHex,
                SortOrder = t.SortOrder,
                Description = t.Description
            })
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
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
                PokemonCount = t.PokemonTags.Count(pt => pt.Pokemon.UserId == userId),
                Category = t.Category.ToString(),
                ColorHex = t.ColorHex,
                SortOrder = t.SortOrder,
                Description = t.Description
            })
            .FirstOrDefaultAsync();
    }

    public async Task<TagDto?> CreateTagAsync(int userId, CreateTagRequest request)
    {
        var existing = await _db.Tags.AnyAsync(t => t.Name == request.Name && t.UserId == userId);
        if (existing) return null;

        var category = TagCategory.Uncategorized;
        if (!string.IsNullOrEmpty(request.Category))
            Enum.TryParse(request.Category, true, out category);

        var tag = new TagEntity
        {
            Name = request.Name,
            UserId = userId,
            Category = category,
            ColorHex = request.ColorHex,
            Description = request.Description
        };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            PokemonCount = 0,
            Category = tag.Category.ToString(),
            ColorHex = tag.ColorHex,
            SortOrder = tag.SortOrder,
            Description = tag.Description
        };
    }

    public async Task<TagDto?> UpdateTagAsync(int userId, int tagId, UpdateTagRequest request)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
        if (tag == null) return null;

        tag.Name = request.Name;

        if (request.Category != null && Enum.TryParse<TagCategory>(request.Category, true, out var cat))
            tag.Category = cat;
        if (request.ColorHex != null)
            tag.ColorHex = request.ColorHex;
        if (request.SortOrder.HasValue)
            tag.SortOrder = request.SortOrder.Value;
        if (request.Description != null)
            tag.Description = request.Description;

        await _db.SaveChangesAsync();

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            ImagePath = tag.ImagePath,
            PokemonCount = 0,
            Category = tag.Category.ToString(),
            ColorHex = tag.ColorHex,
            SortOrder = tag.SortOrder,
            Description = tag.Description
        };
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

    public async Task<BulkTagResult> BulkUpdateTagsAsync(int userId, BulkTagRequest request)
    {
        // Resolve target pokemon IDs (optionally including duplicates by SHA)
        var pokemonIds = request.PokemonIds.ToList();

        if (request.IncludeDuplicateFiles)
        {
            var sha256s = await _db.Pokemon
                .Where(p => pokemonIds.Contains(p.Id) && p.UserId == userId)
                .Select(p => p.File!.Sha256)
                .Where(s => s != null)
                .Distinct()
                .ToListAsync();

            var duplicateIds = await _db.Pokemon
                .Where(p => p.UserId == userId && p.File != null && sha256s.Contains(p.File!.Sha256))
                .Select(p => p.Id)
                .ToListAsync();

            pokemonIds = pokemonIds.Union(duplicateIds).ToList();
        }

        // Validate pokemon belong to user
        var validIds = await _db.Pokemon
            .Where(p => pokemonIds.Contains(p.Id) && p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        // Validate tags belong to user
        var userTagIds = await _db.Tags
            .Where(t => t.UserId == null || t.UserId == userId)
            .Select(t => t.Id)
            .ToListAsync();

        int tagsAdded = 0, tagsRemoved = 0;

        // Replace mode: remove all existing tags and add the replace set
        if (request.ReplaceTagIds is { Length: > 0 })
        {
            var existingPt = await _db.PokemonTags
                .Where(pt => validIds.Contains(pt.PokemonId))
                .ToListAsync();
            tagsRemoved = existingPt.Count;
            _db.PokemonTags.RemoveRange(existingPt);

            foreach (var pokemonId in validIds)
            {
                foreach (var tagId in request.ReplaceTagIds.Where(t => userTagIds.Contains(t)))
                {
                    _db.PokemonTags.Add(new PokemonTagEntity { PokemonId = pokemonId, TagId = tagId });
                    tagsAdded++;
                }
            }
        }
        else
        {
            // Remove tags
            if (request.RemoveTagIds is { Length: > 0 })
            {
                var toRemove = await _db.PokemonTags
                    .Where(pt => validIds.Contains(pt.PokemonId) && request.RemoveTagIds.Contains(pt.TagId))
                    .ToListAsync();
                tagsRemoved = toRemove.Count;
                _db.PokemonTags.RemoveRange(toRemove);
            }

            // Add tags
            if (request.AddTagIds is { Length: > 0 })
            {
                var existingPairs = await _db.PokemonTags
                    .Where(pt => validIds.Contains(pt.PokemonId) && request.AddTagIds.Contains(pt.TagId))
                    .Select(pt => new { pt.PokemonId, pt.TagId })
                    .ToListAsync();

                var existingSet = existingPairs.ToHashSet();

                foreach (var pokemonId in validIds)
                {
                    foreach (var tagId in request.AddTagIds.Where(t => userTagIds.Contains(t)))
                    {
                        if (!existingSet.Contains(new { PokemonId = pokemonId, TagId = tagId }))
                        {
                            _db.PokemonTags.Add(new PokemonTagEntity { PokemonId = pokemonId, TagId = tagId });
                            tagsAdded++;
                        }
                    }
                }
            }
        }

        await _db.SaveChangesAsync();

        return new BulkTagResult
        {
            AffectedPokemon = validIds.Count,
            TagsAdded = tagsAdded,
            TagsRemoved = tagsRemoved
        };
    }
}
