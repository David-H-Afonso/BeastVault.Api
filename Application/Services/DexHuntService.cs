using System.Text.Json;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public sealed class DexHuntService(AppDbContext db)
{
    public const int ExportSchemaVersion = 1;
    public const int MaxItemsPerList = 2000;

    public static IReadOnlyList<DexHuntGameDto> GetGames() => Enumerable.Range(1, 9)
        .SelectMany(PokemonGameInfoService.GetGamesByGeneration)
        .Select(game => new DexHuntGameDto(game.GameId, game.Name, game.Generation))
        .Append(new DexHuntGameDto(52, "Legends: Z-A", 9))
        .GroupBy(game => game.Id)
        .Select(group => group.First())
        .OrderBy(game => game.Generation)
        .ThenBy(game => game.Name)
        .ToList();

    public async Task<IReadOnlyList<DexHuntListSummaryDto>> GetListsAsync(int userId)
    {
        var lists = await db.DexHuntLists
            .AsNoTracking()
            .Where(list => list.UserId == userId)
            .Include(list => list.Items)
            .OrderBy(list => list.SortOrder)
            .ThenBy(list => list.Id)
            .ToListAsync();
        return lists.Select(ToSummary).ToList();
    }

    public async Task<DexHuntListSummaryDto> CreateListAsync(int userId, CreateDexHuntListRequest request)
    {
        var name = ValidateText(request.Name, "Name", 100, required: true)!;
        var game = GetGame(request.GameId);
        var description = ValidateText(request.Description, "Description", 500);
        var nextSortOrder = (await db.DexHuntLists
            .Where(list => list.UserId == userId)
            .MaxAsync(list => (int?)list.SortOrder) ?? -1) + 1;
        var now = DateTime.UtcNow;

        var list = new DexHuntListEntity
        {
            UserId = userId,
            Name = name,
            GameId = game.Id,
            GameName = game.Name,
            Description = description,
            SortOrder = nextSortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.DexHuntLists.Add(list);
        await db.SaveChangesAsync();
        return ToSummary(list);
    }

    public async Task<DexHuntListSummaryDto> UpdateListAsync(int userId, int listId, UpdateDexHuntListRequest request)
    {
        var list = await GetOwnedListAsync(userId, listId);
        if (request.Name is not null)
            list.Name = ValidateText(request.Name, "Name", 100, required: true)!;
        if (request.GameId.HasValue)
        {
            var game = GetGame(request.GameId.Value);
            list.GameId = game.Id;
            list.GameName = game.Name;
        }
        list.Description = ValidateText(request.Description, "Description", 500);
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToSummary(list);
    }

    public async Task DeleteListAsync(int userId, int listId)
    {
        var list = await GetOwnedListAsync(userId, listId);
        db.DexHuntLists.Remove(list);
        await db.SaveChangesAsync();
    }

    public async Task ReorderListsAsync(int userId, IReadOnlyList<int>? listIds)
    {
        var lists = await db.DexHuntLists.Where(list => list.UserId == userId).ToListAsync();
        ValidateExactOrder(listIds, lists.Select(list => list.Id), "list");
        var byId = lists.ToDictionary(list => list.Id);
        var now = DateTime.UtcNow;
        for (var index = 0; index < listIds!.Count; index++)
        {
            byId[listIds[index]].SortOrder = index;
            byId[listIds[index]].UpdatedAt = now;
        }
        await db.SaveChangesAsync();
    }

    public async Task<DexHuntListDetailDto> GetListAsync(
        int userId,
        int listId,
        string? search = null,
        string status = "all",
        int? priority = null,
        int? generation = null,
        string? type = null,
        string sortBy = "manual",
        bool descending = false)
    {
        var list = await db.DexHuntLists
            .AsNoTracking()
            .Where(candidate => candidate.Id == listId && candidate.UserId == userId)
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Dex Hunt not found.");

        var speciesIds = list.Items.Select(item => item.SpeciesId).Distinct().ToList();
        var species = await db.PokedexEntries
            .AsNoTracking()
            .Where(entry => speciesIds.Contains(entry.SpeciesId))
            .ToDictionaryAsync(entry => entry.SpeciesId);
        var forms = await db.PokedexPokemon
            .AsNoTracking()
            .Where(form => speciesIds.Contains(form.SpeciesId) && form.IsDefault)
            .OrderBy(form => form.PokemonId)
            .ToListAsync();
        var formBySpecies = forms.GroupBy(form => form.SpeciesId).ToDictionary(group => group.Key, group => group.First());

        IEnumerable<DexHuntItemDto> items = list.Items.Select(item =>
        {
            species.TryGetValue(item.SpeciesId, out var entry);
            formBySpecies.TryGetValue(item.SpeciesId, out var form);
            return new DexHuntItemDto(
                item.Id,
                item.SpeciesId,
                entry?.Name ?? $"species-{item.SpeciesId}",
                entry?.Generation ?? PokemonGameInfoService.GetSpeciesOriginGeneration(item.SpeciesId),
                ParseTypes(form?.Types),
                form is null ? null : PokemonSpritesDto.ForPokemonId(form.PokemonId, form.Name),
                item.Priority,
                item.IsCaught,
                item.Notes,
                item.SortOrder,
                item.AddedAt,
                item.UpdatedAt,
                item.CaughtAt);
        });

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            items = items.Where(item =>
                item.SpeciesName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.SpeciesId.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                (item.Notes?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        items = status.ToLowerInvariant() switch
        {
            "open" => items.Where(item => !item.IsCaught),
            "caught" => items.Where(item => item.IsCaught),
            "all" => items,
            _ => throw new ArgumentException("Status must be all, open, or caught.")
        };
        if (priority.HasValue)
        {
            ValidatePriority(priority.Value);
            items = items.Where(item => item.Priority == priority.Value);
        }
        if (generation.HasValue)
            items = items.Where(item => item.Generation == generation.Value);
        if (!string.IsNullOrWhiteSpace(type))
            items = items.Where(item => item.Types.Contains(type, StringComparer.OrdinalIgnoreCase));

        items = sortBy.ToLowerInvariant() switch
        {
            "manual" => items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id),
            "number" => descending ? items.OrderByDescending(item => item.SpeciesId) : items.OrderBy(item => item.SpeciesId),
            "name" => descending ? items.OrderByDescending(item => item.SpeciesName) : items.OrderBy(item => item.SpeciesName),
            "generation" => descending ? items.OrderByDescending(item => item.Generation).ThenByDescending(item => item.SpeciesId) : items.OrderBy(item => item.Generation).ThenBy(item => item.SpeciesId),
            "priority" => descending ? items.OrderBy(item => item.Priority).ThenBy(item => item.SortOrder) : items.OrderByDescending(item => item.Priority).ThenBy(item => item.SortOrder),
            "added" => descending ? items.OrderByDescending(item => item.AddedAt) : items.OrderBy(item => item.AddedAt),
            "caught" => descending ? items.OrderByDescending(item => item.CaughtAt) : items.OrderBy(item => item.CaughtAt),
            _ => throw new ArgumentException("Unsupported sort option.")
        };

        return new DexHuntListDetailDto(ToSummary(list), items.ToList());
    }

    public async Task<DexHuntItemDto> AddItemAsync(int userId, int listId, AddDexHuntItemRequest request)
    {
        ValidatePriority(request.Priority);
        var notes = ValidateText(request.Notes, "Notes", 500);
        var list = await GetOwnedListAsync(userId, listId);
        if (list.Items.Count >= MaxItemsPerList)
            throw new ArgumentException($"A Dex Hunt can contain at most {MaxItemsPerList} targets.");
        if (await db.DexHuntItems.AnyAsync(item => item.HuntListId == listId && item.SpeciesId == request.SpeciesId))
            throw new ArgumentException("That species is already in this Dex Hunt.");
        var species = await db.PokedexEntries.AsNoTracking().SingleOrDefaultAsync(entry => entry.SpeciesId == request.SpeciesId)
            ?? throw new ArgumentException("Species is not available in the cached Pokédex.");
        var form = await db.PokedexPokemon.AsNoTracking()
            .Where(candidate => candidate.SpeciesId == request.SpeciesId)
            .OrderByDescending(candidate => candidate.IsDefault)
            .ThenBy(candidate => candidate.PokemonId)
            .FirstOrDefaultAsync();
        var nextSortOrder = (await db.DexHuntItems
            .Where(item => item.HuntListId == listId)
            .MaxAsync(item => (int?)item.SortOrder) ?? -1) + 1;
        var now = DateTime.UtcNow;
        var item = new DexHuntItemEntity
        {
            HuntListId = listId,
            SpeciesId = request.SpeciesId,
            Priority = request.Priority,
            Notes = notes,
            SortOrder = nextSortOrder,
            AddedAt = now,
            UpdatedAt = now
        };
        db.DexHuntItems.Add(item);
        list.UpdatedAt = now;
        await db.SaveChangesAsync();
        return new DexHuntItemDto(
            item.Id, item.SpeciesId, species.Name, species.Generation, ParseTypes(form?.Types),
            form is null ? null : PokemonSpritesDto.ForPokemonId(form.PokemonId, form.Name),
            item.Priority, false, item.Notes, item.SortOrder, item.AddedAt, item.UpdatedAt, null);
    }

    public async Task UpdateItemAsync(int userId, int listId, int itemId, UpdateDexHuntItemRequest request)
    {
        var item = await db.DexHuntItems
            .Include(candidate => candidate.HuntList)
            .SingleOrDefaultAsync(candidate => candidate.Id == itemId && candidate.HuntListId == listId && candidate.HuntList.UserId == userId)
            ?? throw new KeyNotFoundException("Dex Hunt target not found.");
        if (request.Priority.HasValue)
        {
            ValidatePriority(request.Priority.Value);
            item.Priority = request.Priority.Value;
        }
        if (request.IsCaught.HasValue && request.IsCaught.Value != item.IsCaught)
        {
            item.IsCaught = request.IsCaught.Value;
            item.CaughtAt = item.IsCaught ? DateTime.UtcNow : null;
        }
        item.Notes = ValidateText(request.Notes, "Notes", 500);
        item.UpdatedAt = DateTime.UtcNow;
        item.HuntList.UpdatedAt = item.UpdatedAt;
        await db.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int userId, int listId, int itemId)
    {
        var item = await db.DexHuntItems
            .Include(candidate => candidate.HuntList)
            .SingleOrDefaultAsync(candidate => candidate.Id == itemId && candidate.HuntListId == listId && candidate.HuntList.UserId == userId)
            ?? throw new KeyNotFoundException("Dex Hunt target not found.");
        db.DexHuntItems.Remove(item);
        item.HuntList.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ReorderItemsAsync(int userId, int listId, IReadOnlyList<int>? itemIds)
    {
        var list = await GetOwnedListAsync(userId, listId);
        var items = await db.DexHuntItems.Where(item => item.HuntListId == listId).ToListAsync();
        ValidateExactOrder(itemIds, items.Select(item => item.Id), "target");
        var byId = items.ToDictionary(item => item.Id);
        var now = DateTime.UtcNow;
        for (var index = 0; index < itemIds!.Count; index++)
        {
            byId[itemIds[index]].SortOrder = index;
            byId[itemIds[index]].UpdatedAt = now;
        }
        list.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    public async Task<DexHuntExportDto> ExportAsync(int userId, int listId)
    {
        var detail = await GetListAsync(userId, listId);
        return new DexHuntExportDto(
            ExportSchemaVersion,
            DateTime.UtcNow,
            new DexHuntExportListDto(
                detail.List.Name,
                new DexHuntExportGameDto(detail.List.GameId, detail.List.GameName),
                detail.List.Description,
                detail.Items.Select(item => new DexHuntExportItemDto(
                    item.SpeciesId,
                    item.SpeciesName,
                    item.Priority,
                    item.IsCaught,
                    item.Notes,
                    item.CaughtAt)).ToList()));
    }

    public async Task<DexHuntListSummaryDto> ImportAsync(int userId, DexHuntExportDto? export)
    {
        if (export is null || export.List is null)
            throw new ArgumentException("The JSON does not contain a Dex Hunt.");
        if (export.SchemaVersion != ExportSchemaVersion)
            throw new ArgumentException($"Unsupported schemaVersion. Expected {ExportSchemaVersion}.");
        if (export.List.Game is null)
            throw new ArgumentException("The JSON does not contain a game.");
        if (export.List.Items is null || export.List.Items.Count > MaxItemsPerList)
            throw new ArgumentException($"A Dex Hunt can contain at most {MaxItemsPerList} targets.");

        var name = ValidateText(export.List.Name, "Name", 100, required: true)!;
        var description = ValidateText(export.List.Description, "Description", 500);
        var game = GetGame(export.List.Game.Id);
        var duplicateSpecies = export.List.Items.GroupBy(item => item.SpeciesId).FirstOrDefault(group => group.Count() > 1);
        if (duplicateSpecies is not null)
            throw new ArgumentException($"Species #{duplicateSpecies.Key} appears more than once.");

        foreach (var item in export.List.Items)
        {
            ValidatePriority(item.Priority);
            ValidateText(item.Notes, "Notes", 500);
            if (item.SpeciesId <= 0)
                throw new ArgumentException("Every target must contain a valid speciesId.");
        }
        var speciesIds = export.List.Items.Select(item => item.SpeciesId).ToList();
        var existingSpecies = await db.PokedexEntries.AsNoTracking()
            .Where(entry => speciesIds.Contains(entry.SpeciesId))
            .Select(entry => entry.SpeciesId)
            .ToListAsync();
        var missingSpecies = speciesIds.Except(existingSpecies).ToList();
        if (missingSpecies.Count > 0)
            throw new ArgumentException($"Species not available in the cached Pokédex: {string.Join(", ", missingSpecies.Select(id => $"#{id}"))}.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var summary = await CreateListAsync(userId, new CreateDexHuntListRequest(name, game.Id, description));
        var now = DateTime.UtcNow;
        db.DexHuntItems.AddRange(export.List.Items.Select((item, index) => new DexHuntItemEntity
        {
            HuntListId = summary.Id,
            SpeciesId = item.SpeciesId,
            Priority = item.Priority,
            IsCaught = item.Caught,
            Notes = ValidateText(item.Notes, "Notes", 500),
            SortOrder = index,
            AddedAt = now,
            UpdatedAt = now,
            CaughtAt = item.Caught ? item.CaughtAt ?? now : null
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return summary with { TotalCount = export.List.Items.Count, CaughtCount = export.List.Items.Count(item => item.Caught) };
    }

    private async Task<DexHuntListEntity> GetOwnedListAsync(int userId, int listId) =>
        await db.DexHuntLists.Include(list => list.Items)
            .SingleOrDefaultAsync(list => list.Id == listId && list.UserId == userId)
            ?? throw new KeyNotFoundException("Dex Hunt not found.");

    private static DexHuntListSummaryDto ToSummary(DexHuntListEntity list) => new(
        list.Id,
        list.Name,
        list.GameId,
        list.GameName,
        list.Description,
        list.SortOrder,
        list.Items.Count,
        list.Items.Count(item => item.IsCaught),
        list.CreatedAt,
        list.UpdatedAt);

    private static DexHuntGameDto GetGame(int gameId) => GetGames().FirstOrDefault(game => game.Id == gameId)
        ?? throw new ArgumentException("Select a supported Pokémon game.");

    private static string? ValidateText(string? value, string field, int maxLength, bool required = false)
    {
        var trimmed = value?.Trim();
        if (required && string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException($"{field} is required.");
        if (trimmed?.Length > maxLength)
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.");
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static void ValidatePriority(int priority)
    {
        if (priority is < 0 or > 2)
            throw new ArgumentException("Priority must be 0 (low), 1 (normal), or 2 (high).");
    }

    private static void ValidateExactOrder(IReadOnlyList<int>? requestedIds, IEnumerable<int> existingIds, string resource)
    {
        var existing = existingIds.Order().ToArray();
        var requested = requestedIds?.Order().ToArray();
        if (requested is null || requested.Distinct().Count() != requested.Length || !requested.SequenceEqual(existing))
            throw new ArgumentException($"The reorder request must contain every {resource} exactly once.");
    }

    private static string[] ParseTypes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<JsonElement>>(json)?
                .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
