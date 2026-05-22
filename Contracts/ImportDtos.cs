namespace BeastVault.Api.Contracts;

public record ImportResultDto
{
    public required string FileName { get; init; }
    public required string Status { get; init; }
    public int? PokemonId { get; init; }
    public string? Message { get; init; }
}
