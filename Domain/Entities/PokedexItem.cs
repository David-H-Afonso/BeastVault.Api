namespace BeastVault.Api.Domain.Entities;

/// <summary>
/// Cached item data from PokeAPI (balls, held items, etc.)
/// </summary>
public class PokedexItem
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public string SpriteUrl { get; set; } = "";
    public string Effect { get; set; } = "";
    public string FlavorText { get; set; } = "";
    public int? FlingPower { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
