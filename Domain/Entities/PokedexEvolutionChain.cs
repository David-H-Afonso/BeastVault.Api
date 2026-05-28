namespace BeastVault.Api.Domain.Entities;

public class PokedexEvolutionChain
{
    public int ChainId { get; set; }
    /// <summary>Full resolved chain JSON from PokeAPI (chain.chain tree with evolution_details).</summary>
    public string ChainJson { get; set; } = "{}";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
