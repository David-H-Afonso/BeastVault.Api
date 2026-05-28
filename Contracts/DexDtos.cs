namespace BeastVault.Api.Contracts;

/// <summary>One row in the national Pokédex grid.</summary>
public record DexGridEntryDto(
    int SpeciesId,
    string Name,
    int Generation,
    bool IsUnlocked,
    int OwnedCount,
    string[] Types,
    PokemonSpritesDto? Sprites,
    bool IsLegendary,
    bool IsMythical,
    /// <summary>True if the user owns at least one shiny of this species.</summary>
    bool HasShiny
);

/// <summary>Paginated national Pokédex grid response.</summary>
public record DexGridResponse(
    IReadOnlyList<DexGridEntryDto> Items,
    int Total,
    int Page,
    int PageSize
);

/// <summary>One Pokémon the user owns of a given species.</summary>
public record DexOwnedPokemonDto(
    int Id,
    string? Nickname,
    bool IsShiny,
    int Level,
    string FormName,
    string OriginGame,
    string SpriteUrl
);

/// <summary>Full detail view for a species — includes user's owned Pokémon.</summary>
public record DexSpeciesDetailDto(
    int SpeciesId,
    string Name,
    string FlavorText,
    string Genus,
    int Generation,
    bool IsLegendary,
    bool IsMythical,
    bool IsBaby,
    string Color,
    string[] Types,
    object Abilities,
    object BaseStats,
    int CaptureRate,
    int BaseHappiness,
    int GenderRate,
    string[] EggGroups,
    /// <summary>Pokédex numbers per game — array of {game, entryNumber} objects.</summary>
    object GameIndices,
    PokemonSpritesDto? Sprites,
    bool IsUnlocked,
    IReadOnlyList<DexOwnedPokemonDto> OwnedPokemon,
    /// <summary>Raw PokeAPI chain node JSON — parsed by the frontend. Null if not cached.</summary>
    string? EvolutionChainJson
);
