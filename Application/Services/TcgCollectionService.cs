using System.Text.Json;
using System.Collections.Concurrent;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public sealed class TcgCollectionService(
    AppDbContext db,
    ITcgDexProvider tcgDex,
    IPokemonTcgIoProvider pokemonTcgIo,
    IUserApiCredentialService credentials)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim SetsSyncLock = new(1, 1);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SetSyncLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly (string Name, int First, int Last)[] Regions =
    [
        ("Kanto", 1, 151),
        ("Johto", 152, 251),
        ("Hoenn", 252, 386),
        ("Sinnoh", 387, 493),
        ("Unova", 494, 649),
        ("Kalos", 650, 721),
        ("Alola", 722, 809),
        ("Galar", 810, 898),
        ("Hisui", 899, 905),
        ("Paldea", 906, 1025)
    ];

    public async Task<IReadOnlyList<TcgSetDto>> GetSetsAsync(
        int userId,
        string? search,
        CancellationToken cancellationToken)
    {
        await EnsureSetsAsync(cancellationToken);
        var query = db.TcgSets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) ||
                (x.NameEn != null && x.NameEn.ToLower().Contains(term)) ||
                x.ProviderSetId.ToLower().Contains(term));
        }

        var sets = await query.OrderByDescending(x => x.ReleaseDate).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var ownership = await db.UserTcgCards.AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.Card.SetId)
            .Select(x => new
            {
                SetId = x.Key,
                Unique = x.Select(entry => entry.CardId).Distinct().Count(),
                Copies = x.Sum(entry => entry.Quantity)
            })
            .ToDictionaryAsync(x => x.SetId, cancellationToken);

        return sets.Select(set =>
        {
            ownership.TryGetValue(set.Id, out var owned);
            return ToSetDto(set, owned?.Unique ?? 0, owned?.Copies ?? 0);
        }).ToList();
    }

    public async Task<TcgCardPageDto?> GetSetCardsAsync(
        int userId,
        string providerSetId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureSetsAsync(cancellationToken);
        var set = await db.TcgSets.SingleOrDefaultAsync(x => x.ProviderSetId == providerSetId, cancellationToken);
        if (set is null) return null;
        await EnsureSetCardsAsync(set, cancellationToken);
        return await QueryCardsAsync(userId, x => x.SetId == set.Id, page, pageSize, cancellationToken);
    }

    public async Task<TcgCardPageDto> SearchCardsAsync(
        int userId,
        string? query,
        int? setId,
        string? number,
        int? speciesId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureSetsAsync(cancellationToken);
        var localQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim().ToLowerInvariant();
        var localNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim().ToLowerInvariant();
        var local = await QueryCardsAsync(
            userId,
            card => (!setId.HasValue || card.SetId == setId.Value) &&
                (localQuery == null || card.Name.ToLower().Contains(localQuery) ||
                    (card.NameEn != null && card.NameEn.ToLower().Contains(localQuery))) &&
                (localNumber == null || card.Number.ToLower() == localNumber) &&
                (!speciesId.HasValue ||
                    card.NationalPokedexNumbersJson == $"[{speciesId.Value}]" ||
                    card.NationalPokedexNumbersJson.StartsWith($"[{speciesId.Value},") ||
                    card.NationalPokedexNumbersJson.EndsWith($",{speciesId.Value}]") ||
                    card.NationalPokedexNumbersJson.Contains($",{speciesId.Value},")),
            page,
            pageSize,
            cancellationToken);

        var shouldFetch = local.Items.Count == 0 ||
            (!string.IsNullOrWhiteSpace(query) && page == 1);
        if (!shouldFetch || (string.IsNullOrWhiteSpace(query) && !speciesId.HasValue && !setId.HasValue))
            return local;

        string? providerSetId = null;
        if (setId.HasValue)
            providerSetId = await db.TcgSets.Where(x => x.Id == setId.Value).Select(x => x.ProviderSetId).SingleOrDefaultAsync(cancellationToken);

        try
        {
            var english = await tcgDex.SearchCardsAsync(query, providerSetId, number, speciesId, page, pageSize, "en", cancellationToken);
            if (speciesId.HasValue)
            {
                english = english.Select(card => card.NationalPokedexNumbers.Count == 0
                    ? card with { NationalPokedexNumbers = [speciesId.Value] }
                    : card).ToList();
            }
            IReadOnlyList<TcgProviderCard> spanish = [];
            try
            {
                spanish = await tcgDex.SearchCardsAsync(query, providerSetId, number, speciesId, page, pageSize, "es", cancellationToken);
                if (speciesId.HasValue)
                {
                    spanish = spanish.Select(card => card.NationalPokedexNumbers.Count == 0
                        ? card with { NationalPokedexNumbers = [speciesId.Value] }
                        : card).ToList();
                }
            }
            catch (HttpRequestException) { }

            await UpsertCardsAsync(english, spanish, cancellationToken);
            var resultIds = english.Select(x => x.Id).Concat(spanish.Select(x => x.Id)).Distinct().ToList();
            var resultCards = await db.TcgCards.AsNoTracking().Include(x => x.Set)
                .Where(x => resultIds.Contains(x.ProviderCardId))
                .ToListAsync(cancellationToken);
            var byProviderId = resultCards.ToDictionary(x => x.ProviderCardId, StringComparer.OrdinalIgnoreCase);
            var orderedCards = resultIds.Where(byProviderId.ContainsKey).Select(id => byProviderId[id]).ToList();
            var owned = await GetOwnedLookupAsync(userId, orderedCards.Select(x => x.Id), cancellationToken);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
            return new TcgCardPageDto(
                orderedCards.Select(x => ToCardDto(x, owned.GetValueOrDefault(x.Id) ?? [])).ToList(),
                Math.Max(1, page),
                normalizedPageSize,
                english.Count >= normalizedPageSize,
                null);
        }
        catch (HttpRequestException) when (local.Items.Count > 0)
        {
            return local;
        }
    }

    public async Task<TcgCardDto?> GetCardAsync(int userId, int cardId, bool refresh, CancellationToken cancellationToken)
    {
        var card = await db.TcgCards.Include(x => x.Set).SingleOrDefaultAsync(x => x.Id == cardId, cancellationToken);
        if (card is null) return null;
        if (refresh || card.DetailedAt is null || card.DetailedAt < DateTime.UtcNow.AddDays(-1))
        {
            try
            {
                await RefreshCardEntityAsync(userId, card, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Preserve and serve the last cached snapshot when providers are unavailable.
            }
            card = await db.TcgCards.AsNoTracking().Include(x => x.Set).SingleAsync(x => x.Id == cardId, cancellationToken);
        }
        return await ToCardDtoAsync(userId, card, cancellationToken);
    }

    public Task<TcgCardPageDto> GetSpeciesCardsAsync(
        int userId,
        int speciesId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        SearchCardsAsync(userId, null, null, null, speciesId, page, pageSize, cancellationToken);

    public async Task<TcgCollectionPageDto> GetCollectionAsync(
        int userId,
        string? query,
        int? setId,
        string? language,
        string? condition,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var source = db.UserTcgCards.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Card).ThenInclude(x => x.Set)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            source = source.Where(x => x.Card.Name.ToLower().Contains(term) ||
                (x.Card.NameEn != null && x.Card.NameEn.ToLower().Contains(term)) ||
                x.Card.Number.ToLower().Contains(term));
        }
        if (setId.HasValue) source = source.Where(x => x.Card.SetId == setId.Value);
        if (!string.IsNullOrWhiteSpace(language)) source = source.Where(x => x.Language == language);
        if (!string.IsNullOrWhiteSpace(condition)) source = source.Where(x => x.Condition == condition);

        var total = await source.CountAsync(cancellationToken);
        var entries = await source.OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ownedByCard = await GetOwnedLookupAsync(userId, entries.Select(x => x.CardId), cancellationToken);
        return new TcgCollectionPageDto(
            entries.Select(x => ToUserCardDto(x, ToCardDto(x.Card, ownedByCard.GetValueOrDefault(x.CardId) ?? []))).ToList(),
            page,
            pageSize,
            total);
    }

    public async Task<UserCardDto> AddAsync(
        int userId,
        AddTcgCollectionEntryRequest request,
        CancellationToken cancellationToken)
    {
        ValidateEntry(request.Variant, request.Condition, request.Language, request.Quantity);
        var card = await db.TcgCards.Include(x => x.Set).SingleOrDefaultAsync(x => x.Id == request.CardId, cancellationToken)
            ?? throw new KeyNotFoundException("Card not found.");
        if (card.DetailedAt is null)
        {
            try { await RefreshCardEntityAsync(userId, card, cancellationToken); }
            catch (HttpRequestException) { }
        }
        var variant = NormalizeToken(request.Variant, 80);
        var condition = NormalizeToken(request.Condition, 30).ToUpperInvariant();
        var language = NormalizeToken(request.Language, 20).ToUpperInvariant();
        var entry = await db.UserTcgCards.SingleOrDefaultAsync(x =>
            x.UserId == userId && x.CardId == request.CardId && x.Variant == variant &&
            x.Condition == condition && x.Language == language, cancellationToken);
        if (entry is null)
        {
            entry = new UserTcgCardEntity
            {
                UserId = userId,
                CardId = request.CardId,
                Variant = variant,
                Condition = condition,
                Language = language,
                Quantity = request.Quantity,
                Notes = NormalizeNotes(request.Notes)
            };
            db.UserTcgCards.Add(entry);
        }
        else
        {
            entry.Quantity = Math.Min(9999, entry.Quantity + request.Quantity);
            if (request.Notes is not null) entry.Notes = NormalizeNotes(request.Notes);
            entry.UpdatedAt = DateTime.UtcNow;
        }
        entry.Card = card;
        await db.SaveChangesAsync(cancellationToken);
        var owned = await db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId && x.CardId == card.Id).ToListAsync(cancellationToken);
        return ToUserCardDto(entry, ToCardDto(card, owned));
    }

    public async Task<UserCardDto?> UpdateAsync(
        int userId,
        int entryId,
        UpdateTcgCollectionEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await db.UserTcgCards.Include(x => x.Card).ThenInclude(x => x.Set)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == entryId, cancellationToken);
        if (entry is null) return null;
        var variant = request.Variant is null ? entry.Variant : NormalizeToken(request.Variant, 80);
        var condition = request.Condition is null ? entry.Condition : NormalizeToken(request.Condition, 30).ToUpperInvariant();
        var language = request.Language is null ? entry.Language : NormalizeToken(request.Language, 20).ToUpperInvariant();
        var quantity = request.Quantity ?? entry.Quantity;
        ValidateEntry(variant, condition, language, quantity);

        var collision = await db.UserTcgCards.SingleOrDefaultAsync(x => x.Id != entry.Id && x.UserId == userId &&
            x.CardId == entry.CardId && x.Variant == variant && x.Condition == condition && x.Language == language,
            cancellationToken);
        if (collision is not null)
        {
            collision.Quantity = Math.Min(9999, collision.Quantity + quantity);
            collision.Notes = request.Notes is null ? collision.Notes : NormalizeNotes(request.Notes);
            collision.UpdatedAt = DateTime.UtcNow;
            collision.Card = entry.Card;
            db.UserTcgCards.Remove(entry);
            entry = collision;
        }
        else
        {
            entry.Variant = variant;
            entry.Condition = condition;
            entry.Language = language;
            entry.Quantity = quantity;
            if (request.Notes is not null) entry.Notes = NormalizeNotes(request.Notes);
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        var owned = await db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId && x.CardId == entry.CardId).ToListAsync(cancellationToken);
        return ToUserCardDto(entry, ToCardDto(entry.Card, owned));
    }

    public async Task<bool> DeleteAsync(int userId, int entryId, CancellationToken cancellationToken)
    {
        var entry = await db.UserTcgCards.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == entryId, cancellationToken);
        if (entry is null) return false;
        db.UserTcgCards.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TcgCollectionStatsDto> GetStatsAsync(int userId, CancellationToken cancellationToken)
    {
        var entries = await db.UserTcgCards.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Card).ThenInclude(x => x.Set)
            .ToListAsync(cancellationToken);
        var unique = entries.Select(x => x.CardId).Distinct().Count();
        var copies = entries.Sum(x => x.Quantity);
        var totalEur = entries.Sum(x => GetVariantPrice(x.Card.VariantPricesEurJson, x.Variant, x.Card.PriceEur) * x.Quantity ?? 0);
        var totalUsd = entries.Sum(x => GetVariantPrice(x.Card.VariantPricesUsdJson, x.Variant, x.Card.PriceUsd) * x.Quantity ?? 0);
        var ownedSpecies = entries.SelectMany(x => Deserialize<int>(x.Card.NationalPokedexNumbersJson)).Where(x => x is >= 1 and <= 1025).ToHashSet();

        var national = BuildDexProgress("National", 1, 1025, ownedSpecies);
        var regions = Regions.Select(region => BuildDexProgress(region.Name, region.First, region.Last, ownedSpecies)).ToList();
        var setProgress = entries.GroupBy(x => x.Card.Set)
            .Select(group => new TcgSetProgressDto(
                group.Key.Id,
                group.Key.ProviderSetId,
                group.Key.Name,
                group.Select(x => x.CardId).Distinct().Count(),
                group.Key.Total,
                Percent(group.Select(x => x.CardId).Distinct().Count(), group.Key.Total)))
            .OrderByDescending(x => x.CompletionPercent).ThenBy(x => x.Name).ToList();
        var ownedLookup = entries.GroupBy(x => x.CardId).ToDictionary(x => x.Key, x => (IReadOnlyList<UserTcgCardEntity>)x.ToList());
        var top = entries.OrderByDescending(x =>
                GetVariantPrice(x.Card.VariantPricesEurJson, x.Variant, x.Card.PriceEur) * x.Quantity ?? 0)
            .Take(10)
            .Select(x => ToUserCardDto(x, ToCardDto(x.Card, ownedLookup[x.CardId])))
            .ToList();
        return new TcgCollectionStatsDto(unique, copies, totalEur, totalUsd, national, regions, setProgress, top);
    }

    public async Task<TcgSyncResultDto> SyncCatalogAsync(bool includeCards, CancellationToken cancellationToken)
    {
        var errors = 0;
        await SetsSyncLock.WaitAsync(cancellationToken);
        try
        {
            var english = await tcgDex.GetSetsAsync("en", cancellationToken);
            IReadOnlyList<TcgProviderSet> spanish = [];
            try { spanish = await tcgDex.GetSetsAsync("es", cancellationToken); }
            catch (HttpRequestException) { errors++; }
            var spanishById = spanish.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var providerSet in english)
            {
                var entity = await db.TcgSets.SingleOrDefaultAsync(x => x.Provider == "tcgdex" && x.ProviderSetId == providerSet.Id, cancellationToken);
                entity ??= new TcgSetEntity { ProviderSetId = providerSet.Id };
                if (db.Entry(entity).State == EntityState.Detached) db.TcgSets.Add(entity);
                ApplySet(entity, providerSet, spanishById.GetValueOrDefault(providerSet.Id));
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SetsSyncLock.Release();
        }

        if (includeCards)
        {
            var sets = await db.TcgSets.OrderBy(x => x.ReleaseDate).ToListAsync(cancellationToken);
            foreach (var set in sets)
            {
                try
                {
                    await EnsureSetCardsAsync(set, cancellationToken, force: true);
                }
                catch (HttpRequestException)
                {
                    errors++;
                }
            }
        }

        return new TcgSyncResultDto(
            await db.TcgSets.CountAsync(cancellationToken),
            await db.TcgCards.CountAsync(cancellationToken),
            errors,
            includeCards);
    }

    private async Task EnsureSetsAsync(CancellationToken cancellationToken)
    {
        var newest = await db.TcgSets.MaxAsync(x => (DateTime?)x.SyncedAt, cancellationToken);
        if (newest.HasValue && newest.Value > DateTime.UtcNow.AddDays(-7)) return;
        await SetsSyncLock.WaitAsync(cancellationToken);
        try
        {
            newest = await db.TcgSets.AsNoTracking().MaxAsync(x => (DateTime?)x.SyncedAt, cancellationToken);
            if (newest.HasValue && newest.Value > DateTime.UtcNow.AddDays(-7)) return;
            IReadOnlyList<TcgProviderSet> english;
            try { english = await tcgDex.GetSetsAsync("en", cancellationToken); }
            catch (HttpRequestException) when (newest.HasValue) { return; }
            IReadOnlyList<TcgProviderSet> spanish = [];
            try { spanish = await tcgDex.GetSetsAsync("es", cancellationToken); }
            catch (HttpRequestException) { }
            var spanishById = spanish.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var providerSet in english)
            {
                var entity = await db.TcgSets.SingleOrDefaultAsync(x => x.Provider == "tcgdex" && x.ProviderSetId == providerSet.Id, cancellationToken);
                entity ??= new TcgSetEntity { ProviderSetId = providerSet.Id };
                if (db.Entry(entity).State == EntityState.Detached) db.TcgSets.Add(entity);
                ApplySet(entity, providerSet, spanishById.GetValueOrDefault(providerSet.Id));
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SetsSyncLock.Release();
        }
    }

    private async Task EnsureSetCardsAsync(TcgSetEntity set, CancellationToken cancellationToken, bool force = false)
    {
        if (!force && set.CardsSyncedAt > DateTime.UtcNow.AddDays(-7) && await db.TcgCards.AnyAsync(x => x.SetId == set.Id, cancellationToken)) return;
        var syncLock = SetSyncLocks.GetOrAdd(set.ProviderSetId, _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            await db.Entry(set).ReloadAsync(cancellationToken);
            if (!force && set.CardsSyncedAt > DateTime.UtcNow.AddDays(-7) && await db.TcgCards.AnyAsync(x => x.SetId == set.Id, cancellationToken)) return;
            var english = await tcgDex.GetSetAsync(set.ProviderSetId, "en", cancellationToken);
            if (english is null) return;
            TcgProviderSet? spanish = null;
            try { spanish = await tcgDex.GetSetAsync(set.ProviderSetId, "es", cancellationToken); }
            catch (HttpRequestException) { }
            ApplySet(set, english, spanish);
            await UpsertCardsAsync(english.Cards, spanish?.Cards ?? [], cancellationToken);
            set.CardsSyncedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task UpsertCardsAsync(
        IReadOnlyList<TcgProviderCard> english,
        IReadOnlyList<TcgProviderCard> spanish,
        CancellationToken cancellationToken)
    {
        var spanishById = spanish.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var all = english.Concat(spanish.Where(x => english.All(item => !item.Id.Equals(x.Id, StringComparison.OrdinalIgnoreCase)))).ToList();
        foreach (var providerCard in all)
        {
            var set = await EnsureSetShellAsync(providerCard.SetId, providerCard.SetName, cancellationToken);
            var entity = await db.TcgCards.SingleOrDefaultAsync(x => x.Provider == "tcgdex" && x.ProviderCardId == providerCard.Id, cancellationToken);
            entity ??= new TcgCardEntity { ProviderCardId = providerCard.Id, SetId = set.Id, Name = providerCard.Name, Number = providerCard.Number };
            if (db.Entry(entity).State == EntityState.Detached) db.TcgCards.Add(entity);
            ApplyCard(entity, providerCard, spanishById.GetValueOrDefault(providerCard.Id));
            entity.SetId = set.Id;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TcgSetEntity> EnsureSetShellAsync(string providerSetId, string setName, CancellationToken cancellationToken)
    {
        var set = await db.TcgSets.SingleOrDefaultAsync(x => x.Provider == "tcgdex" && x.ProviderSetId == providerSetId, cancellationToken);
        if (set is not null) return set;
        set = new TcgSetEntity { ProviderSetId = providerSetId, Name = string.IsNullOrWhiteSpace(setName) ? providerSetId : setName };
        db.TcgSets.Add(set);
        await db.SaveChangesAsync(cancellationToken);
        return set;
    }

    private async Task RefreshCardEntityAsync(int userId, TcgCardEntity entity, CancellationToken cancellationToken)
    {
        var english = await tcgDex.GetCardAsync(entity.ProviderCardId, "en", cancellationToken);
        TcgProviderCard? spanish = null;
        try { spanish = await tcgDex.GetCardAsync(entity.ProviderCardId, "es", cancellationToken); }
        catch (HttpRequestException) { }
        if (english is not null || spanish is not null) ApplyCard(entity, english ?? spanish!, spanish);

        var apiKey = await credentials.GetTcgApiKeyAsync(userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var enriched = await pokemonTcgIo.GetCardAsync(entity.PokemonTcgIoId ?? entity.ProviderCardId, apiKey, cancellationToken);
                if (enriched is not null) ApplyEnrichment(entity, enriched);
            }
            catch (HttpRequestException) { }
        }
        entity.DetailedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TcgCardPageDto> QueryCardsAsync(
        int userId,
        System.Linq.Expressions.Expression<Func<TcgCardEntity, bool>> predicate,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        int? totalOverride = null)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var source = db.TcgCards.AsNoTracking().Where(predicate);
        var total = totalOverride ?? await source.CountAsync(cancellationToken);
        var cards = await source.Include(x => x.Set).OrderBy(x => x.Set.ReleaseDate).ThenBy(x => x.Number)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = cards.Count > pageSize || page * pageSize < total;
        cards = cards.Take(pageSize).ToList();
        var owned = await GetOwnedLookupAsync(userId, cards.Select(x => x.Id), cancellationToken);
        return new TcgCardPageDto(cards.Select(x => ToCardDto(x, owned.GetValueOrDefault(x.Id) ?? [])).ToList(), page, pageSize, hasMore, total);
    }

    private async Task<TcgCardDto> ToCardDtoAsync(int userId, TcgCardEntity card, CancellationToken cancellationToken)
    {
        var owned = await db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId && x.CardId == card.Id).ToListAsync(cancellationToken);
        return ToCardDto(card, owned);
    }

    private async Task<Dictionary<int, IReadOnlyList<UserTcgCardEntity>>> GetOwnedLookupAsync(
        int userId,
        IEnumerable<int> cardIds,
        CancellationToken cancellationToken)
    {
        var ids = cardIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var entries = await db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId && ids.Contains(x.CardId)).ToListAsync(cancellationToken);
        return entries.GroupBy(x => x.CardId).ToDictionary(x => x.Key, x => (IReadOnlyList<UserTcgCardEntity>)x.ToList());
    }

    private static TcgCardDto ToCardDto(TcgCardEntity card, IReadOnlyList<UserTcgCardEntity> owned) => new(
        card.Id, card.ProviderCardId, card.Name, card.NameEn, card.Number, card.Rarity, card.Artist,
        card.ImageSmall, card.ImageLarge, Deserialize<int>(card.NationalPokedexNumbersJson),
        Deserialize<string>(card.VariantsJson), card.SetId, card.Set.ProviderSetId, card.Set.Name,
        new TcgPriceDto(
            card.PriceEur,
            card.PriceUsd,
            card.PriceUpdatedAt,
            card.CardmarketUrl,
            card.TcgplayerUrl,
            DeserializeDictionary(card.VariantPricesEurJson),
            DeserializeDictionary(card.VariantPricesUsdJson)),
        owned.Select(x => new TcgOwnedEntryDto(x.Id, x.Variant, x.Condition, x.Language, x.Quantity, x.Notes)).ToList(),
        owned.Sum(x => x.Quantity));

    private static UserCardDto ToUserCardDto(UserTcgCardEntity entry, TcgCardDto card)
    {
        var entity = entry.Card;
        var eur = GetVariantPrice(entity.VariantPricesEurJson, entry.Variant, entity.PriceEur);
        var usd = GetVariantPrice(entity.VariantPricesUsdJson, entry.Variant, entity.PriceUsd);
        return new UserCardDto(entry.Id, card, entry.Variant, entry.Condition, entry.Language, entry.Quantity,
            entry.Notes, entry.AddedAt, eur, usd, eur * entry.Quantity, usd * entry.Quantity);
    }

    private TcgDexProgressDto BuildDexProgress(string name, int first, int last, IReadOnlySet<int> owned)
    {
        var ownedCount = Enumerable.Range(first, last - first + 1).Count(owned.Contains);
        var missing = Enumerable.Range(first, last - first + 1).Where(id => !owned.Contains(id))
            .Select(id => new TcgMissingSpeciesDto(id, PkHexStringService.GetSpeciesName(id))).ToList();
        return new TcgDexProgressDto(name, ownedCount, last - first + 1, Percent(ownedCount, last - first + 1), missing);
    }

    private static void ApplySet(TcgSetEntity entity, TcgProviderSet english, TcgProviderSet? spanish)
    {
        entity.Name = spanish?.Name ?? english.Name;
        entity.NameEn = english.Name;
        entity.Series = spanish?.Series ?? english.Series;
        entity.PrintedTotal = english.PrintedTotal;
        entity.Total = english.Total;
        entity.ReleaseDate = english.ReleaseDate;
        entity.SymbolUrl = spanish?.SymbolUrl ?? english.SymbolUrl;
        entity.LogoUrl = spanish?.LogoUrl ?? english.LogoUrl;
        entity.SyncedAt = DateTime.UtcNow;
    }

    private static void ApplyCard(TcgCardEntity entity, TcgProviderCard english, TcgProviderCard? spanish)
    {
        var localized = spanish ?? english;
        entity.Name = localized.Name;
        entity.NameEn = english.Name;
        entity.Number = localized.Number;
        entity.Rarity = localized.Rarity ?? english.Rarity;
        entity.Artist = localized.Artist ?? english.Artist;
        entity.ImageSmall = localized.ImageSmall ?? english.ImageSmall;
        entity.ImageLarge = localized.ImageLarge ?? english.ImageLarge;
        entity.NationalPokedexNumbersJson = JsonSerializer.Serialize(localized.NationalPokedexNumbers.Count > 0 ? localized.NationalPokedexNumbers : english.NationalPokedexNumbers, JsonOptions);
        entity.VariantsJson = JsonSerializer.Serialize(localized.Variants.Count > 0 ? localized.Variants : english.Variants, JsonOptions);
        entity.PriceEur = localized.PriceEur ?? english.PriceEur;
        entity.PriceUsd = localized.PriceUsd ?? english.PriceUsd;
        entity.VariantPricesEurJson = JsonSerializer.Serialize(localized.VariantPricesEur.Count > 0 ? localized.VariantPricesEur : english.VariantPricesEur, JsonOptions);
        entity.VariantPricesUsdJson = JsonSerializer.Serialize(localized.VariantPricesUsd.Count > 0 ? localized.VariantPricesUsd : english.VariantPricesUsd, JsonOptions);
        entity.PriceUpdatedAt = localized.PriceUpdatedAt ?? english.PriceUpdatedAt;
        entity.CardmarketUrl = localized.CardmarketUrl ?? english.CardmarketUrl;
        entity.TcgplayerUrl = localized.TcgplayerUrl ?? english.TcgplayerUrl;
        entity.CardmarketUrl ??= $"https://www.cardmarket.com/en/Pokemon/Products/Search?searchString={Uri.EscapeDataString(localized.Name)}";
        entity.TcgplayerUrl ??= $"https://www.tcgplayer.com/search/pokemon/product?productLineName=pokemon&q={Uri.EscapeDataString(localized.Name)}";
        entity.SyncedAt = DateTime.UtcNow;
        if (entity.PriceUpdatedAt.HasValue || localized.Artist is not null) entity.DetailedAt = DateTime.UtcNow;
    }

    private static void ApplyEnrichment(TcgCardEntity entity, TcgProviderCard value)
    {
        entity.PokemonTcgIoId = value.Id;
        entity.PriceEur = value.PriceEur ?? entity.PriceEur;
        entity.PriceUsd = value.PriceUsd ?? entity.PriceUsd;
        if (value.VariantPricesEur.Count > 0) entity.VariantPricesEurJson = JsonSerializer.Serialize(value.VariantPricesEur, JsonOptions);
        if (value.VariantPricesUsd.Count > 0) entity.VariantPricesUsdJson = JsonSerializer.Serialize(value.VariantPricesUsd, JsonOptions);
        entity.PriceUpdatedAt = value.PriceUpdatedAt ?? entity.PriceUpdatedAt;
        entity.CardmarketUrl = value.CardmarketUrl ?? entity.CardmarketUrl;
        entity.TcgplayerUrl = value.TcgplayerUrl ?? entity.TcgplayerUrl;
        entity.VariantsJson = JsonSerializer.Serialize(Deserialize<string>(entity.VariantsJson).Concat(value.Variants).Distinct().ToList(), JsonOptions);
    }

    private static TcgSetDto ToSetDto(TcgSetEntity set, int unique, int copies) => new(
        set.Id, set.ProviderSetId, set.Name, set.NameEn, set.Series, set.PrintedTotal, set.Total,
        set.ReleaseDate, set.SymbolUrl, set.LogoUrl, unique, copies, Percent(unique, set.Total));

    private static decimal? GetVariantPrice(string json, string variant, decimal? fallback)
    {
        var prices = DeserializeDictionary(json);
        if (prices.TryGetValue(variant, out var exact)) return exact;
        var normalized = TcgDexProvider.NormalizeVariant(variant);
        if (prices.TryGetValue(normalized, out exact)) return exact;
        return fallback;
    }

    private static Dictionary<string, decimal> DeserializeDictionary(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions) ?? new(StringComparer.OrdinalIgnoreCase); }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static List<T> Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) => (Math.Clamp(page, 1, 10_000), Math.Clamp(pageSize, 1, 100));
    private static decimal Percent(int owned, int total) => total <= 0 ? 0 : Math.Round((decimal)owned / total * 100, 2);

    private static void ValidateEntry(string variant, string condition, string language, int quantity)
    {
        if (string.IsNullOrWhiteSpace(variant) || string.IsNullOrWhiteSpace(condition) || string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Variant, condition and language are required.");
        if (quantity is < 1 or > 9999) throw new ArgumentException("Quantity must be between 1 and 9999.");
    }

    private static string NormalizeToken(string value, int maxLength)
    {
        var result = value.Trim();
        if (result.Length == 0 || result.Length > maxLength) throw new ArgumentException("A collection value is invalid.");
        return result;
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var normalized = notes.Trim();
        if (normalized.Length > 2000) throw new ArgumentException("Notes cannot exceed 2000 characters.");
        return normalized;
    }
}
