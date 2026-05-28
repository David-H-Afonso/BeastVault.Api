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
    /// <summary>Numeric chain ID extracted from EvolutionChainUrl. Null until populated.</summary>
    public int? EvolutionChainId { get; set; }
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
    /// <summary>Raw JSON of moves array from PokeAPI — includes all learn methods and version groups. Populated during species fetch.</summary>
    public string MovesJson { get; set; } = "[]";
    /// <summary>Local path served via /sprites/pokemon/{PokemonId}.png — null until sprites are downloaded.</summary>
    public string? SpriteLocalPath { get; set; }
    /// <summary>Local path for official artwork — null until downloaded.</summary>
    public string? ArtworkLocalPath { get; set; }
    /// <summary>Raw PNG bytes of the front_default sprite stored in DB for offline/portable use. Null until synced.</summary>
    public byte[]? SpriteData { get; set; }
    /// <summary>Raw PNG bytes of the official artwork stored in DB for offline/portable use. Null until synced.</summary>
    public byte[]? ArtworkData { get; set; }
    /// <summary>Raw PNG bytes of the front_shiny sprite stored in DB for offline/portable use. Null until synced.</summary>
    public byte[]? ShinyData { get; set; }
    /// <summary>Raw PNG bytes of the Pokémon HOME 3D sprite (other.home.front_default). Null until synced.</summary>
    public byte[]? HomeSpriteData { get; set; }
    /// <summary>Raw PNG bytes of the Pokémon HOME shiny sprite (other.home.front_shiny). Null until synced.</summary>
    public byte[]? HomeShinyData { get; set; }
    /// <summary>Animated GIF bytes of the Showdown sprite (other.showdown.front_default). Null until synced.</summary>
    public byte[]? ShowdownData { get; set; }
    /// <summary>Animated GIF bytes of the Showdown shiny sprite (other.showdown.front_shiny). Null until synced.</summary>
    public byte[]? ShowdownShinyData { get; set; }
    /// <summary>Raw PNG bytes of the official artwork shiny sprite (other.official-artwork.front_shiny). Null until synced.</summary>
    public byte[]? ArtworkShinyData { get; set; }
    /// <summary>PNG bytes of the pokesprite gen8 regular sprite (from msikma/pokesprite). Null until synced.</summary>
    public byte[]? GithubSpriteData { get; set; }
    /// <summary>PNG bytes of the pokesprite gen8 shiny sprite (from msikma/pokesprite). Null until synced.</summary>
    public byte[]? GithubShinySpriteData { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
