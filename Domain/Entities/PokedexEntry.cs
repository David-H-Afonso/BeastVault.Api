namespace BeastVault.Api.Domain.Entities;

public class PokedexEntry
{
    public int SpeciesId { get; set; }
    public string Name { get; set; } = "";
    public string LocalizedNames { get; set; } = "{}";
    public string Genus { get; set; } = "";
    public string FlavorText { get; set; } = "";
    public int Generation { get; set; }
    public string Color { get; set; } = "";
    public string Shape { get; set; } = "";
    public string Habitat { get; set; } = "";
    public string GrowthRate { get; set; } = "";
    public int CaptureRate { get; set; }
    public int BaseHappiness { get; set; }
    public int HatchCounter { get; set; }
    public int GenderRate { get; set; }
    public bool IsLegendary { get; set; }
    public bool IsMythical { get; set; }
    public bool IsBaby { get; set; }
    public bool HasGenderDifferences { get; set; }
    public bool FormsSwitchable { get; set; }
    public string EggGroups { get; set; } = "[]";
    public string Varieties { get; set; } = "[]";
    public string EvolutionChainUrl { get; set; } = "";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class PokedexPokemon
{
    public int PokemonId { get; set; }
    public int SpeciesId { get; set; }
    public string Name { get; set; } = "";
    public int Height { get; set; }
    public int Weight { get; set; }
    public int BaseExperience { get; set; }
    public int Order { get; set; }
    public bool IsDefault { get; set; }
    public string Types { get; set; } = "[]";
    public string Abilities { get; set; } = "[]";
    public string BaseStats { get; set; } = "{}";
    public string Sprites { get; set; } = "{}";
    public string Cries { get; set; } = "{}";
    public string GameIndices { get; set; } = "[]";
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
