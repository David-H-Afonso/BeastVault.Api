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

/// <summary>Localized name of a species.</summary>
public record DexLocalizedNameDto(string Language, string Name, string? RomanizedName = null);

/// <summary>Pokédex flavor text entry per language and game version.</summary>
public record DexFlavorEntryDto(string Language, string GameVersion, string Text, string Source);

/// <summary>Encounter location in a specific game.</summary>
public record DexLocationDto(string Game, string Location, string? Method, string Source);

/// <summary>Sprite set for a specific generation.</summary>
public record DexGenerationSpritesDto(
    int Generation,
    string Label,
    string? NormalUrl,
    string? ShinyUrl,
    string? BackUrl,
    string? BackShinyUrl,
    string Source
);

/// <summary>Form variant with its own types, abilities, and sprites.</summary>
public record DexFormDto(
    int PokemonId,
    string Name,
    bool IsDefault,
    string[] Types,
    object[] Abilities,
    PokemonSpritesDto? Sprites
);

/// <summary>Cache status for enrichment data.</summary>
public record DexCacheStatusDto(
    bool PokeApiCached,
    bool BulbapediaCached,
    string? BulbapediaStatus,
    bool BulbapediaNormalized = false,
    string? BulbapediaNormalizedStatus = null,
    int BulbapediaEntriesCount = 0,
    int BulbapediaLocationsCount = 0,
    int BulbapediaSpritesCount = 0
);

/// <summary>Full detail view for a species — includes user's owned Pokémon and enriched data.</summary>
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
    string? EvolutionChainJson,
    // --- Enriched data ---
    IReadOnlyList<DexLocalizedNameDto> LocalizedNames,
    string? JapaneseName,
    string? JapaneseRomanized,
    string? NameMeaning,
    IReadOnlyList<DexFlavorEntryDto> FlavorEntries,
    IReadOnlyList<DexLocationDto> Locations,
    IReadOnlyList<DexGenerationSpritesDto> SpritesByGeneration,
    IReadOnlyList<DexFormDto> Forms,
    DexCacheStatusDto CacheStatus
);
