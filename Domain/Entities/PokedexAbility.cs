namespace BeastVault.Api.Domain.Entities;

public class PokedexAbility
{
    public int AbilityId { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Effect { get; set; } = "";
    public string ShortEffect { get; set; } = "";
    public string FlavorText { get; set; } = "";
    public int Generation { get; set; }
    public bool IsMainSeries { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
