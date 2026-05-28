namespace BeastVault.Api.Domain.Entities;

public class PokedexType
{
    public int TypeId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>JSON object with double_damage_to, half_damage_to, no_damage_to, double_damage_from, half_damage_from, no_damage_from arrays of type names.</summary>
    public string DamageRelations { get; set; } = "{}";
    public int Generation { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
