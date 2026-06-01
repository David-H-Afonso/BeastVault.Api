namespace BeastVault.Api.Domain.Entities;

public class PokemonTagEntity
{
    public int PokemonId { get; set; }
    public int TagId { get; set; }
    public int SortOrder { get; set; } = 0;

    public PokemonEntity Pokemon { get; set; } = null!;
    public TagEntity Tag { get; set; } = null!;
}
