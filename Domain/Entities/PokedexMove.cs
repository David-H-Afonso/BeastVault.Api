namespace BeastVault.Api.Domain.Entities;

/// <summary>
/// Cached move data from PokeAPI
/// </summary>
public class PokedexMove
{
    public int MoveId { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Type { get; set; } = "";
    public string DamageClass { get; set; } = "";
    public int? Power { get; set; }
    public int? Accuracy { get; set; }
    public int PP { get; set; }
    public int Priority { get; set; }
    public string Effect { get; set; } = "";
    public string FlavorText { get; set; } = "";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
