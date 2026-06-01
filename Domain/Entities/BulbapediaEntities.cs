namespace BeastVault.Api.Domain.Entities;

public enum CacheSource
{
    PokeApi = 0,
    Bulbapedia = 1
}

public enum ParseStatus
{
    Pending = 0,
    Success = 1,
    PartialSuccess = 2,
    Failed = 3
}

/// <summary>
/// Cached Bulbapedia page parse result
/// </summary>
public class BulbapediaCache
{
    public int Id { get; set; }
    public int SpeciesId { get; set; }
    public required string PageTitle { get; set; }
    public required string PageUrl { get; set; }
    public int? RevisionId { get; set; }
    public int? PageId { get; set; }
    public string? RawContent { get; set; }
    public string? RawHtml { get; set; }
    public string? ParsedSections { get; set; }
    public ParseStatus Status { get; set; } = ParseStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NormalizedAt { get; set; }
    public ParseStatus NormalizedStatus { get; set; } = ParseStatus.Pending;
    public string? NormalizedError { get; set; }
    public string? NameMeaning { get; set; }
    public int EntriesCount { get; set; }
    public int LocationsCount { get; set; }
    public int SpritesCount { get; set; }
}

/// <summary>
/// Per-language, per-game Pokédex flavor text entry (replaces single FlavorText on PokedexEntry)
/// </summary>
public class PokedexFlavorEntry
{
    public int Id { get; set; }
    public int SpeciesId { get; set; }
    public required string Language { get; set; }
    public required string GameVersion { get; set; }
    public required string Text { get; set; }
    public CacheSource Source { get; set; } = CacheSource.PokeApi;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Location/method where a Pokémon can be obtained in a specific game
/// </summary>
public class PokedexLocation
{
    public int Id { get; set; }
    public int SpeciesId { get; set; }
    public required string Game { get; set; }
    public required string Location { get; set; }
    public string? Method { get; set; }
    public CacheSource Source { get; set; } = CacheSource.Bulbapedia;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Locally cached image downloaded from an external source
/// </summary>
public class CachedImage
{
    public int Id { get; set; }
    public required string SourceUrl { get; set; }
    public required string LocalPath { get; set; }
    public required string ImageType { get; set; }
    public int? SpeciesId { get; set; }
    public int? PokemonId { get; set; }
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-game sprite provenance normalized from Bulbapedia and served through local routes.
/// </summary>
public class PokedexSpriteEntry
{
    public int Id { get; set; }
    public int SpeciesId { get; set; }
    public int? PokemonId { get; set; }
    public int Generation { get; set; }
    public required string GameSlug { get; set; }
    public required string DisplayLabel { get; set; }
    public string? NormalLocalPath { get; set; }
    public string? ShinyLocalPath { get; set; }
    public string? BackLocalPath { get; set; }
    public string? BackShinyLocalPath { get; set; }
    public string? SourceUrl { get; set; }
    public CacheSource Source { get; set; } = CacheSource.Bulbapedia;
    public int SortOrder { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
