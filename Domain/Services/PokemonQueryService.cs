using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Domain.Specifications;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Domain.Services;

/// <summary>
/// Service for building and applying Pokemon queries with advanced filtering and sorting
/// </summary>
public static class PokemonQueryService
{
    /// <summary>
    /// Build and apply a complete Pokemon query with filters and sorting
    /// </summary>
    public static IQueryable<PokemonEntity> BuildQuery(
        IQueryable<PokemonEntity> baseQuery,
        AdvancedPokemonQuery queryParams)
    {
        // Apply specifications (filters)
        var specifications = BuildSpecifications(queryParams);
        var filteredQuery = ApplySpecifications(baseQuery, specifications);

        // Apply sorting
        var sortedQuery = ApplySorting(filteredQuery, queryParams);

        return sortedQuery;
    }

    /// <summary>
    /// Build specifications from query parameters
    /// </summary>
    private static IEnumerable<IPokemonSpecification> BuildSpecifications(AdvancedPokemonQuery query)
    {
        var specifications = new List<IPokemonSpecification>();

        // Text search
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            specifications.Add(new TextSearchSpecification(query.Search));
        }

        // Pokedex number 
        if (query.PokedexNumber.HasValue)
        {
            specifications.Add(new PokedexNumberSpecification(query.PokedexNumber.Value));
        }

        // Species name
        if (!string.IsNullOrWhiteSpace(query.SpeciesName))
        {
            specifications.Add(new SpeciesNameSpecification(query.SpeciesName));
        }

        // Nickname
        if (!string.IsNullOrWhiteSpace(query.Nickname))
        {
            specifications.Add(new NicknameSpecification(query.Nickname));
        }

        // Shiny status
        if (query.IsShiny.HasValue)
        {
            specifications.Add(new ShinySpecification(query.IsShiny.Value));
        }

        // Form
        if (query.Form.HasValue)
        {
            specifications.Add(new FormSpecification(query.Form.Value));
        }

        // Gender
        if (query.Gender.HasValue)
        {
            specifications.Add(new GenderSpecification(query.Gender.Value));
        }

        // Origin generation
        if (query.OriginGeneration.HasValue)
        {
            specifications.Add(new OriginGenerationSpecification(query.OriginGeneration.Value));
        }

        // Captured generation
        if (query.CapturedGeneration.HasValue)
        {
            specifications.Add(new CapturedGenerationSpecification(query.CapturedGeneration.Value));
        }

        // Pokeball
        if (query.PokeballId.HasValue)
        {
            specifications.Add(new PokeballSpecification(query.PokeballId.Value));
        }

        // Type filtering
        var typeOptions = query.ToTypeFilterOptions();
        if (typeOptions != null)
        {
            specifications.Add(new TypeSpecification(typeOptions));
        }

        // Level range
        if (query.MinLevel.HasValue || query.MaxLevel.HasValue)
        {
            specifications.Add(new LevelRangeSpecification(query.MinLevel, query.MaxLevel));
        }

        // Legacy filters
        if (query.OriginGame.HasValue)
        {
            specifications.Add(new LegacyOriginGameSpecification(query.OriginGame.Value));
        }

        if (query.TeraType.HasValue)
        {
            specifications.Add(new TeraTypeSpecification(query.TeraType.Value));
        }

        if (query.HeldItemId.HasValue)
        {
            specifications.Add(new HeldItemSpecification(query.HeldItemId.Value));
        }

        return specifications;
    }

    /// <summary>
    /// Apply all specifications to the query
    /// </summary>
    private static IQueryable<PokemonEntity> ApplySpecifications(
        IQueryable<PokemonEntity> query,
        IEnumerable<IPokemonSpecification> specifications)
    {
        var compositeSpec = new CompositeSpecification(specifications);
        return compositeSpec.Apply(query);
    }

    /// <summary>
    /// Apply sorting to the query
    /// </summary>
    private static IQueryable<PokemonEntity> ApplySorting(
        IQueryable<PokemonEntity> query,
        AdvancedPokemonQuery queryParams)
    {
        var sortOptions = queryParams.GetAllSortOptions().ToList();

        if (sortOptions.Any())
        {
            return PokemonSortingService.ApplyMultipleSort(query, sortOptions);
        }

        // Default sort
        return query.OrderByDescending(p => p.Id);
    }

    /// <summary>
    /// Get query statistics for debugging/monitoring
    /// </summary>
    public static PokemonQueryStats GetQueryStats(AdvancedPokemonQuery query)
    {
        var specifications = BuildSpecifications(query).ToList();
        var sortOptions = query.GetAllSortOptions().ToList();

        return new PokemonQueryStats
        {
            FilterCount = specifications.Count,
            SortCount = sortOptions.Count,
            HasTextSearch = !string.IsNullOrWhiteSpace(query.Search),
            HasTypeFilter = query.ToTypeFilterOptions() != null,
            HasLevelFilter = query.MinLevel.HasValue || query.MaxLevel.HasValue,
            HasGenerationFilter = query.OriginGeneration.HasValue || query.CapturedGeneration.HasValue,
            UsesComplexSorting = sortOptions.Count > 1
        };
    }
}

/// <summary>
/// Statistics about a Pokemon query for monitoring and optimization
/// </summary>
public record PokemonQueryStats
{
    public int FilterCount { get; init; }
    public int SortCount { get; init; }
    public bool HasTextSearch { get; init; }
    public bool HasTypeFilter { get; init; }
    public bool HasLevelFilter { get; init; }
    public bool HasGenerationFilter { get; init; }
    public bool UsesComplexSorting { get; init; }
}
