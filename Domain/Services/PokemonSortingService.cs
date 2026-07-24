using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure.Services;
using System.Linq.Expressions;

namespace BeastVault.Api.Domain.Services;

/// <summary>
/// Service for applying sorting to Pokemon queries
/// </summary>
public static class PokemonSortingService
{
    /// <summary>
    /// Apply sorting to a Pokemon query
    /// </summary>
    public static IOrderedQueryable<PokemonEntity> ApplySort(IQueryable<PokemonEntity> query, PokemonSortOptions options)
    {
        return options.SortBy switch
        {
            PokemonSortField.Id => ApplySort(query, p => p.Id, options.Direction),
            PokemonSortField.PokedexNumber => ApplySort(query, p => p.SpeciesId, options.Direction),
            PokemonSortField.SpeciesName => ApplySortBySpeciesName(query, options.Direction),
            PokemonSortField.Nickname => ApplySort(query, p => p.Nickname ?? "", options.Direction),
            PokemonSortField.Level => ApplySort(query, p => p.Level, options.Direction),
            PokemonSortField.OriginGeneration => ApplySortByOriginGeneration(query, options.Direction),
            PokemonSortField.CapturedGeneration => ApplySortByCapturedGeneration(query, options.Direction),
            PokemonSortField.Pokeball => ApplySort(query, p => p.BallId, options.Direction),
            PokemonSortField.Gender => ApplySort(query, p => p.Gender, options.Direction),
            PokemonSortField.IsShiny => ApplySort(query, p => p.IsShiny ? 1 : 0, options.Direction), // Convert bool to int for proper sorting
            PokemonSortField.Form => ApplySort(query, p => p.Form, options.Direction),
            PokemonSortField.CreatedAt => ApplySort(query, p => p.File.ImportedAt, options.Direction),
            PokemonSortField.Favorite => ApplySort(query, p => p.Favorite ? 1 : 0, options.Direction), // Convert bool to int for proper sorting
            _ => query.OrderByDescending(p => p.Id) // Default fallback
        };
    }

    /// <summary>
    /// Apply sorting with direction to a query
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplySort<TKey>(
        IQueryable<PokemonEntity> query,
        Expression<Func<PokemonEntity, TKey>> keySelector,
        SortDirection direction)
    {
        return direction == SortDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }

    /// <summary>
    /// Sort by species name using PKHeX resolution
    /// Note: This creates an in-memory sort which may not be efficient for large datasets
    /// Consider creating a computed column or lookup table for production use
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplySortBySpeciesName(
        IQueryable<PokemonEntity> query,
        SortDirection direction)
    {
        // For now, we'll sort by SpeciesId as a proxy for species name
        // In a production system, you might want to pre-compute species names
        // or use a lookup table for better performance
        return ApplySort(query, p => p.SpeciesId, direction);
    }

    /// <summary>
    /// Sort by origin generation
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplySortByOriginGeneration(
        IQueryable<PokemonEntity> query,
        SortDirection direction)
    {
        // We can create a computed column or use a CASE statement in SQL
        // For now, we'll use a simple approach that maps common game IDs to generations
        return direction == SortDirection.Ascending
            ? query.OrderBy(p => p.OriginGame < 4 ? 1 :
                                p.OriginGame < 8 ? 2 :
                                p.OriginGame < 13 ? 3 :
                                p.OriginGame < 18 ? 4 :
                                p.OriginGame < 24 ? 5 :
                                p.OriginGame < 30 ? 6 :
                                p.OriginGame < 44 ? 7 :
                                p.OriginGame < 51 ? 8 : 9)
            : query.OrderByDescending(p => p.OriginGame < 4 ? 1 :
                                         p.OriginGame < 8 ? 2 :
                                         p.OriginGame < 13 ? 3 :
                                         p.OriginGame < 18 ? 4 :
                                         p.OriginGame < 24 ? 5 :
                                         p.OriginGame < 30 ? 6 :
                                         p.OriginGame < 44 ? 7 :
                                         p.OriginGame < 51 ? 8 : 9);
    }

    /// <summary>
    /// Sort by captured generation (generation where the species was introduced)
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplySortByCapturedGeneration(
        IQueryable<PokemonEntity> query,
        SortDirection direction)
    {
        // Map species to their introduction generation
        return direction == SortDirection.Ascending
            ? query.OrderBy(p => p.SpeciesId <= 151 ? 1 :
                                p.SpeciesId <= 251 ? 2 :
                                p.SpeciesId <= 386 ? 3 :
                                p.SpeciesId <= 493 ? 4 :
                                p.SpeciesId <= 649 ? 5 :
                                p.SpeciesId <= 721 ? 6 :
                                p.SpeciesId <= 809 ? 7 :
                                p.SpeciesId <= 905 ? 8 : 9)
            : query.OrderByDescending(p => p.SpeciesId <= 151 ? 1 :
                                         p.SpeciesId <= 251 ? 2 :
                                         p.SpeciesId <= 386 ? 3 :
                                         p.SpeciesId <= 493 ? 4 :
                                         p.SpeciesId <= 649 ? 5 :
                                         p.SpeciesId <= 721 ? 6 :
                                         p.SpeciesId <= 809 ? 7 :
                                         p.SpeciesId <= 905 ? 8 : 9);
    }

    /// <summary>
    /// Apply multiple sorts to a query (for complex sorting scenarios)
    /// </summary>
    public static IOrderedQueryable<PokemonEntity> ApplyMultipleSort(
        IQueryable<PokemonEntity> query,
        IEnumerable<PokemonSortOptions> sortOptions)
    {
        var sortList = sortOptions.ToList();
        if (!sortList.Any())
        {
            return query.OrderByDescending(p => p.Id);
        }

        var orderedQuery = ApplySort(query, sortList.First());

        foreach (var sortOption in sortList.Skip(1))
        {
            orderedQuery = ApplyThenBy(orderedQuery, sortOption);
        }

        return orderedQuery;
    }

    /// <summary>
    /// Apply a then-by sort to an already ordered query
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplyThenBy(
        IOrderedQueryable<PokemonEntity> query,
        PokemonSortOptions options)
    {
        return options.SortBy switch
        {
            PokemonSortField.Id => ApplyThenBy(query, p => p.Id, options.Direction),
            PokemonSortField.PokedexNumber => ApplyThenBy(query, p => p.SpeciesId, options.Direction),
            PokemonSortField.Nickname => ApplyThenBy(query, p => p.Nickname ?? "", options.Direction),
            PokemonSortField.Level => ApplyThenBy(query, p => p.Level, options.Direction),
            PokemonSortField.Pokeball => ApplyThenBy(query, p => p.BallId, options.Direction),
            PokemonSortField.Gender => ApplyThenBy(query, p => p.Gender, options.Direction),
            PokemonSortField.IsShiny => ApplyThenBy(query, p => p.IsShiny ? 1 : 0, options.Direction), // Convert bool to int
            PokemonSortField.Form => ApplyThenBy(query, p => p.Form, options.Direction),
            PokemonSortField.CreatedAt => ApplyThenBy(query, p => p.Id, options.Direction), // Use Id as proxy for creation time
            PokemonSortField.Favorite => ApplyThenBy(query, p => p.Favorite ? 1 : 0, options.Direction), // Convert bool to int
            _ => query // For complex sorts, just return the original query
        };
    }

    /// <summary>
    /// Apply then-by sorting with direction
    /// </summary>
    private static IOrderedQueryable<PokemonEntity> ApplyThenBy<TKey>(
        IOrderedQueryable<PokemonEntity> query,
        Expression<Func<PokemonEntity, TKey>> keySelector,
        SortDirection direction)
    {
        return direction == SortDirection.Ascending
            ? query.ThenBy(keySelector)
            : query.ThenByDescending(keySelector);
    }
}
