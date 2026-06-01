using System.ComponentModel.DataAnnotations;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Contracts;

public record TagDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? ImagePath { get; init; }
    public int PokemonCount { get; init; }
    public string Category { get; init; } = "Uncategorized";
    public string? ColorHex { get; init; }
    public int SortOrder { get; init; }
    public string? Description { get; init; }
}

public record CreateTagRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    public string? Category { get; init; }
    public string? ColorHex { get; init; }
    public string? Description { get; init; }
}

public record UpdateTagRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    public string? Category { get; init; }
    public string? ColorHex { get; init; }
    public int? SortOrder { get; init; }
    public string? Description { get; init; }
}

public record TagImageUrlRequest
{
    [Required]
    public required string ImageUrl { get; init; }
}

public record PokemonTagsRequest
{
    public required int[] TagIds { get; init; }
}

public record BulkTagRequest
{
    [Required]
    public required int[] PokemonIds { get; init; }
    public int[]? AddTagIds { get; init; }
    public int[]? RemoveTagIds { get; init; }
    public int[]? ReplaceTagIds { get; init; }
    public bool IncludeDuplicateFiles { get; init; } = false;
}

public record BulkTagResult
{
    public int AffectedPokemon { get; init; }
    public int TagsAdded { get; init; }
    public int TagsRemoved { get; init; }
}
