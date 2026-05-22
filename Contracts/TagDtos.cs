using System.ComponentModel.DataAnnotations;

namespace BeastVault.Api.Contracts;

public record TagDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? ImagePath { get; init; }
    public int PokemonCount { get; init; }
}

public record CreateTagRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }
}

public record UpdateTagRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }
}

public record PokemonTagsRequest
{
    public required int[] TagIds { get; init; }
}
