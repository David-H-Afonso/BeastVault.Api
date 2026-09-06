using BeastVault.Api.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace BeastVault.Api.Contracts;

/// <summary>
/// Advanced query parameters for filtering and sorting Pokemon
/// </summary>
public record AdvancedPokemonQuery
{
    #region Basic Filters

    /// <summary>
    /// Text search across nickname, OT name, and notes
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Filter by specific Pokedex number (Species ID)
    /// </summary>
    public int? PokedexNumber { get; init; }

    /// <summary>
    /// Filter by species name (partial match)
    /// </summary>
    public string? SpeciesName { get; init; }

    /// <summary>
    /// Filter by nickname (partial match)
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// Filter by shiny status
    /// </summary>
    public bool? IsShiny { get; init; }

    /// <summary>
    /// Filter by favorite status.
    /// </summary>
    public bool? Favorite { get; init; }

    /// <summary>
    /// Filter by form ID
    /// </summary>
    public int? Form { get; init; }

    /// <summary>
    /// Filter by gender (0 = undefined, 1 = male, 2 = female)
    /// </summary>
    public int? Gender { get; init; }

    /// <summary>
    /// Filter Pokemon that can Gigantamax.
    /// </summary>
    public bool? CanGigantamax { get; init; }

    /// <summary>
    /// Filter Pokemon that are currently displayed as Mega-capable through held items.
    /// </summary>
    public bool? HasMegaStone { get; init; }

    /// <summary>
    /// Filter egg status.
    /// </summary>
    public bool? IsEgg { get; init; }

    #endregion

    #region Generation Filters

    /// <summary>
    /// Filter by origin generation (generation where species was first introduced)
    /// Example: Rowlet is from generation 7 (Alola)
    /// </summary>
    public int? OriginGeneration { get; init; }

    /// <summary>
    /// Filter by captured generation (generation where Pokemon was caught/obtained)
    /// Example: Rowlet caught in Scarlet/Violet would be generation 9
    /// </summary>
    public int? CapturedGeneration { get; init; }

    /// <summary>Filter by the region where the species was introduced.</summary>
    public string? OriginRegion { get; init; }

    /// <summary>Filter by the region associated with the Pokémon's origin game.</summary>
    public string? CapturedRegion { get; init; }

    #endregion

    #region Equipment Filters

    /// <summary>
    /// Filter by Pokeball ID
    /// </summary>
    public int? PokeballId { get; init; }

    /// <summary>
    /// Filter by held item ID
    /// </summary>
    public int? HeldItemId { get; init; }

    #endregion

    #region Type Filters

    /// <summary>
    /// Primary type ID for type filtering
    /// </summary>
    public int? PrimaryType { get; init; }

    /// <summary>
    /// Secondary type ID for type filtering
    /// </summary>
    public int? SecondaryType { get; init; }

    /// <summary>
    /// Type filter mode (how to apply type filters)
    /// </summary>
    public TypeFilterMode? TypeFilterMode { get; init; }

    /// <summary>
    /// Whether to enforce exact type order for dual-type filtering
    /// </summary>
    public bool? EnforceTypeOrder { get; init; }

    #endregion

    #region Level and Stats Filters

    /// <summary>
    /// Minimum level filter
    /// </summary>
    [Range(1, 100)]
    public int? MinLevel { get; init; }

    /// <summary>
    /// Maximum level filter
    /// </summary>
    [Range(1, 100)]
    public int? MaxLevel { get; init; }

    /// <summary>Filter by the displayed Secret ID.</summary>
    [Range(0, 999999)]
    public int? Sid { get; init; }

    /// <summary>Filter by original trainer name (partial, case-insensitive).</summary>
    [MaxLength(12)]
    public string? OtName { get; init; }

    #endregion

    #region Sorting

    /// <summary>
    /// Primary sort field
    /// </summary>
    public PokemonSortField? SortBy { get; init; }

    /// <summary>
    /// Primary sort direction
    /// </summary>
    public SortDirection? SortDirection { get; init; }

    /// <summary>
    /// Secondary sort field (optional)
    /// </summary>
    public PokemonSortField? ThenSortBy { get; init; }

    /// <summary>
    /// Secondary sort direction
    /// </summary>
    public SortDirection? ThenSortDirection { get; init; }

    #endregion

    #region Pagination

    /// <summary>
    /// Number of items to skip (for pagination)
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Skip { get; init; } = 0;

    /// <summary>
    /// Number of items to take (max recommended: 100)
    /// </summary>
    [Range(1, 500)]
    public int Take { get; init; } = 50;

    #endregion

    #region Legacy Support

    /// <summary>
    /// Legacy species ID filter (use PokedexNumber instead)
    /// </summary>
    [Obsolete("Use PokedexNumber instead")]
    public int? SpeciesId { get; init; }

    /// <summary>
    /// Legacy ball ID filter (use PokeballId instead)
    /// </summary>
    [Obsolete("Use PokeballId instead")]
    public int? BallId { get; init; }

    /// <summary>
    /// Legacy origin game filter
    /// </summary>
    public int? OriginGame { get; init; }

    /// <summary>
    /// Legacy tera type filter
    /// </summary>
    public int? TeraType { get; init; }

    #endregion

    #region Tag Filters

    /// <summary>
    /// Filter by tag IDs (Pokémon must have ALL specified tags)
    /// </summary>
    public int[]? TagIds { get; init; }

    /// <summary>
    /// Filter by tag names (Pokémon must have ALL specified tags)
    /// </summary>
    public string[]? TagNames { get; init; }

    /// <summary>
    /// Filter by tag IDs where Pokémon must have ANY of the specified tags
    /// </summary>
    public int[]? AnyTagIds { get; init; }

    /// <summary>
    /// Filter by tag names where Pokémon must have ANY of the specified tags
    /// </summary>
    public string[]? AnyTagNames { get; init; }

    /// <summary>
    /// Exclude Pokémon that have any of these tag IDs.
    /// </summary>
    public int[]? ExcludedTagIds { get; init; }

    /// <summary>
    /// Filter Pokémon that have no tags
    /// </summary>
    public bool? HasNoTags { get; init; }

    #endregion

    #region Internal Search Helpers

    /// <summary>
    /// Internal species IDs resolved from text search.
    /// </summary>
    public int[]? SearchSpeciesIds { get; init; }

    /// <summary>
    /// Internal held item IDs resolved from text search.
    /// </summary>
    public int[]? SearchHeldItemIds { get; init; }

    /// <summary>
    /// Internal move IDs resolved from text search.
    /// </summary>
    public int[]? SearchMoveIds { get; init; }

    /// <summary>
    /// Optional explicit Pokemon IDs, used by box hydration.
    /// </summary>
    public int[]? PokemonIds { get; init; }

    #endregion

    /// <summary>
    /// Convert to type filter options
    /// </summary>
    public TypeFilterOptions? ToTypeFilterOptions()
    {
        if (PrimaryType == null && SecondaryType == null)
            return null;

        return new TypeFilterOptions
        {
            PrimaryType = PrimaryType,
            SecondaryType = SecondaryType,
            Mode = TypeFilterMode ?? Domain.ValueObjects.TypeFilterMode.HasAnyType,
            EnforceTypeOrder = EnforceTypeOrder ?? false
        };
    }

    /// <summary>
    /// Get primary sort options
    /// </summary>
    public PokemonSortOptions GetPrimarySortOptions()
    {
        return new PokemonSortOptions
        {
            SortBy = SortBy ?? PokemonSortField.Id,
            Direction = SortDirection ?? Domain.ValueObjects.SortDirection.Descending
        };
    }

    /// <summary>
    /// Get secondary sort options (if specified)
    /// </summary>
    public PokemonSortOptions? GetSecondarySortOptions()
    {
        if (ThenSortBy == null)
            return null;

        return new PokemonSortOptions
        {
            SortBy = ThenSortBy.Value,
            Direction = ThenSortDirection ?? Domain.ValueObjects.SortDirection.Ascending
        };
    }

    /// <summary>
    /// Get all sort options as a list
    /// </summary>
    public IEnumerable<PokemonSortOptions> GetAllSortOptions()
    {
        yield return GetPrimarySortOptions();

        var secondary = GetSecondarySortOptions();
        if (secondary != null)
            yield return secondary;
    }
}
