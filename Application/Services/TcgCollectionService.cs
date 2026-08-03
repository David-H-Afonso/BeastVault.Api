using System.Text.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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
    IUserApiCredentialService credentials,
    TcgAssetCacheService assets)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxBatchCards = 10;
    private static readonly Regex CollectorReferencePattern = new(
        @"^\s*(?:(?<code>[A-Za-z][A-Za-z0-9-]{0,11})\s+)?(?<number>\d+)(?:\s*/\s*(?<total>\d+))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                x.ProviderSetId.ToLower().Contains(term) ||
                (x.OfficialCode != null && x.OfficialCode.ToLower().Contains(term)));
        }

        var sets = (await query.OrderByDescending(x => x.ReleaseDate).ThenBy(x => x.Name).ToListAsync(cancellationToken))
            .GroupBy(x => $"{x.Provider}:{x.ProviderSetId}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        var ownership = await db.UserTcgCards.AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => new { x.Card.Provider, x.Card.Set.ProviderSetId })
            .Select(x => new
            {
                Provider = x.Key.Provider,
                ProviderSetId = x.Key.ProviderSetId,
                Unique = x.Select(entry => entry.CardId).Distinct().Count(),
                Copies = x.Sum(entry => entry.Quantity)
            })
            .ToDictionaryAsync(x => $"{x.Provider}:{x.ProviderSetId}", cancellationToken);

        return sets.Select(set =>
        {
            ownership.TryGetValue($"{set.Provider}:{set.ProviderSetId}", out var owned);
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

    public async Task<IReadOnlyList<int>?> GetSetCardIdsForAssetCacheAsync(
        string providerSetId,
        CancellationToken cancellationToken)
    {
        await EnsureSetsAsync(cancellationToken);
        var set = await db.TcgSets.SingleOrDefaultAsync(x => x.ProviderSetId == providerSetId, cancellationToken);
        if (set is null) return null;
        await EnsureSetCardsAsync(set, cancellationToken);
        return await db.TcgCards.AsNoTracking()
            .Where(x => x.SetId == set.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetAllCardIdsForAssetCacheAsync(CancellationToken cancellationToken)
    {
        await EnsureSetsAsync(cancellationToken);
        var sets = await db.TcgSets.OrderBy(x => x.ReleaseDate).ToListAsync(cancellationToken);
        foreach (var set in sets)
            await EnsureSetCardsAsync(set, cancellationToken);

        return await db.TcgCards.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);
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
        var queryValue = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var numberValue = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        var selectedSet = setId.HasValue
            ? await db.TcgSets.SingleOrDefaultAsync(x => x.Id == setId.Value, cancellationToken)
            : null;
        if (setId.HasValue && selectedSet is null) return EmptyCardPage(page, pageSize);

        CollectorReference? collector = null;
        var queryIsCollector = false;
        if (TryParseCollectorReference(numberValue, out var numberReference))
        {
            collector = numberReference;
            if (collector.OfficialCode is not null)
            {
                var referencedSet = await ResolveOfficialSetAsync(collector.OfficialCode, cancellationToken);
                if (referencedSet is null) return EmptyCardPage(page, pageSize);
                if (selectedSet is not null && selectedSet.Id != referencedSet.Id) return EmptyCardPage(page, pageSize);
                selectedSet = referencedSet;
            }
        }
        else if (TryParseCollectorReference(queryValue, out var queryReference))
        {
            if (queryReference.OfficialCode is null)
            {
                collector = queryReference;
                queryIsCollector = true;
            }
            else
            {
                var referencedSet = await ResolveOfficialSetAsync(queryReference.OfficialCode, cancellationToken);
                if (referencedSet is not null)
                {
                    if (selectedSet is not null && selectedSet.Id != referencedSet.Id) return EmptyCardPage(page, pageSize);
                    collector = queryReference;
                    queryIsCollector = true;
                    selectedSet = referencedSet;
                }
            }
        }

        if (collector?.PrintedTotal is int printedTotal && selectedSet is not null)
        {
            if (selectedSet.PrintedTotal <= 0)
            {
                var providerSet = await tcgDex.GetSetAsync(selectedSet.ProviderSetId, "en", cancellationToken);
                if (providerSet is not null)
                {
                    ApplySet(selectedSet, providerSet, null);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            if (selectedSet.PrintedTotal <= 0)
                throw new ArgumentException($"The printed total for {selectedSet.Name} is unavailable.");
            if (selectedSet.PrintedTotal != printedTotal)
                throw new ArgumentException($"Collector reference total does not match {selectedSet.Name}.");
        }

        var nameQuery = queryIsCollector ? null : queryValue;
        var textNumber = collector is null ? numberValue : null;
        var local = await QuerySearchCardsAsync(
            userId,
            nameQuery,
            selectedSet?.Id,
            textNumber,
            collector,
            speciesId,
            page,
            pageSize,
            cancellationToken);

        var shouldFetch = local.Items.Count == 0 ||
            (!string.IsNullOrWhiteSpace(query) && page == 1) ||
            (collector is not null && selectedSet is not null && page == 1);
        if (!shouldFetch || (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(number) &&
            !speciesId.HasValue && !setId.HasValue))
            return local;

        try
        {
            IReadOnlyList<TcgProviderCard> english;
            IReadOnlyList<TcgProviderCard> spanish = [];
            if (collector is not null && selectedSet is not null)
            {
                var localId = local.Items.FirstOrDefault() is { } localCard
                    ? GetCollectorNumerator(localCard.Number)
                    : collector.LocalId;
                var englishCard = await tcgDex.GetSetCardAsync(selectedSet.ProviderSetId, localId, "en", cancellationToken);
                english = englishCard is null ? [] : [englishCard];
                try
                {
                    var spanishCard = await tcgDex.GetSetCardAsync(selectedSet.ProviderSetId, localId, "es", cancellationToken);
                    spanish = spanishCard is null ? [] : [spanishCard];
                }
                catch (HttpRequestException) { }
            }
            else
            {
                english = await tcgDex.SearchCardsAsync(
                    nameQuery,
                    selectedSet?.ProviderSetId,
                    collector?.LocalId ?? textNumber,
                    speciesId,
                    page,
                    pageSize,
                    "en",
                    cancellationToken);
                try
                {
                    spanish = await tcgDex.SearchCardsAsync(
                        nameQuery,
                        selectedSet?.ProviderSetId,
                        collector?.LocalId ?? textNumber,
                        speciesId,
                        page,
                        pageSize,
                        "es",
                        cancellationToken);
                }
                catch (HttpRequestException) { }
            }

            english = await PostFilterProviderCardsAsync(english, nameQuery, selectedSet, textNumber, collector, speciesId, cancellationToken);
            spanish = await PostFilterProviderCardsAsync(spanish, nameQuery, selectedSet, textNumber, collector, speciesId, cancellationToken);
            if (speciesId.HasValue)
            {
                english = AddMissingSpecies(english, speciesId.Value);
                spanish = AddMissingSpecies(spanish, speciesId.Value);
            }

            await UpsertCardsAsync(english, spanish, cancellationToken);
            var resultIds = english.Select(x => x.Id).Concat(spanish.Select(x => x.Id)).Distinct().ToList();
            if (resultIds.Count == 0) return local;
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
        if (refresh || card.DetailedAt is null)
        {
            await RefreshCardEntityAsync(userId, card, cancellationToken);
            card = await db.TcgCards.AsNoTracking().Include(x => x.Set).SingleAsync(x => x.Id == cardId, cancellationToken);
        }
        return await ToCardDtoAsync(userId, card, cancellationToken);
    }

    public async Task<TcgCardRefreshResultDto?> RefreshCardAsync(
        int userId,
        int cardId,
        CancellationToken cancellationToken)
    {
        var card = await db.TcgCards.Include(x => x.Set).SingleOrDefaultAsync(x => x.Id == cardId, cancellationToken);
        if (card is null) return null;
        var error = await RefreshCardEntityAsync(userId, card, cancellationToken);
        card = await db.TcgCards.AsNoTracking().Include(x => x.Set).SingleAsync(x => x.Id == cardId, cancellationToken);
        return new TcgCardRefreshResultDto(cardId, error is null, error, await ToCardDtoAsync(userId, card, cancellationToken));
    }

    public async Task<TcgBatchRefreshResultDto> RefreshCardsAsync(
        int userId,
        TcgBatchRefreshRequest request,
        CancellationToken cancellationToken)
    {
        var requestedIds = (request.CardIds ?? []).Where(x => x > 0).Distinct().ToList();
        var truncated = false;
        var requested = requestedIds.Count;
        if (request.OwnedOnly)
        {
            var ownedQuery = db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId);
            if (requestedIds.Count > 0) ownedQuery = ownedQuery.Where(x => requestedIds.Contains(x.CardId));
            var ownedCardIds = ownedQuery.Select(x => x.CardId).Distinct();
            requested = requestedIds.Count > 0
                ? requestedIds.Count
                : await ownedCardIds.CountAsync(cancellationToken);
            var ownedIds = await ownedCardIds.OrderBy(x => x)
                .Take(MaxBatchCards + 1).ToListAsync(cancellationToken);
            truncated = ownedIds.Count > MaxBatchCards;
            requestedIds = ownedIds.Take(MaxBatchCards).ToList();
        }
        else if (requestedIds.Count > MaxBatchCards)
        {
            truncated = true;
            requestedIds = requestedIds.Take(MaxBatchCards).ToList();
        }
        else if (requestedIds.Count == 0)
        {
            throw new ArgumentException("At least one card id is required unless ownedOnly is true.");
        }

        var results = new List<TcgCardRefreshResultDto>(requestedIds.Count);
        foreach (var id in requestedIds)
        {
            var result = await RefreshCardAsync(userId, id, cancellationToken);
            results.Add(result ?? new TcgCardRefreshResultDto(id, false, "Card not found.", null));
        }

        return new TcgBatchRefreshResultDto(results, requested, results.Count, truncated);
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
        var source = db.UserTcgCards.AsNoTracking().Where(x => x.UserId == userId);
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

        var groups = source.GroupBy(x => x.CardId)
            .Select(group => new { CardId = group.Key, SortId = group.Max(x => x.Id) });
        var total = await groups.CountAsync(cancellationToken);
        var pageGroups = await groups.OrderByDescending(x => x.SortId).ThenBy(x => x.CardId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var cardIds = pageGroups.Select(x => x.CardId).ToList();
        List<UserTcgCardEntity> entries = cardIds.Count == 0
            ? []
            : await db.UserTcgCards.AsNoTracking()
                .Where(x => x.UserId == userId && cardIds.Contains(x.CardId))
                .Include(x => x.Card).ThenInclude(x => x.Set)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);
        var entriesByCard = entries.GroupBy(x => x.CardId).ToDictionary(x => x.Key, x => x.ToList());
        var items = new List<TcgCollectionCardDto>(pageGroups.Count);
        foreach (var group in pageGroups)
        {
            if (!entriesByCard.TryGetValue(group.CardId, out var cardEntries) || cardEntries.Count == 0) continue;
            var card = ToCardDto(cardEntries[0].Card, cardEntries);
            var itemEntries = cardEntries.Select(ToCollectionEntryDto).ToList();
            items.Add(new TcgCollectionCardDto(
                card,
                itemEntries,
                cardEntries.Sum(x => x.Quantity),
                itemEntries.Sum(x => x.TotalValueEur ?? 0),
                itemEntries.Sum(x => x.TotalValueUsd ?? 0),
                cardEntries.Max(x => x.UpdatedAt)));
        }

        return new TcgCollectionPageDto(
            items,
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

    public async Task<int> DeleteCardAsync(int userId, int cardId, CancellationToken cancellationToken)
    {
        var entries = await db.UserTcgCards
            .Where(x => x.UserId == userId && x.CardId == cardId)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return 0;
        db.UserTcgCards.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public async Task<DeleteTcgCardsResultDto> DeleteCardsAsync(
        int userId,
        DeleteTcgCardsRequest request,
        CancellationToken cancellationToken)
    {
        var cardIds = (request.CardIds ?? []).Where(x => x > 0).Distinct().ToList();
        if (cardIds.Count == 0) throw new ArgumentException("At least one card id is required.");
        if (cardIds.Count > 100) throw new ArgumentException("A maximum of 100 cards can be deleted at once.");

        var entries = await db.UserTcgCards
            .Where(x => x.UserId == userId && cardIds.Contains(x.CardId))
            .ToListAsync(cancellationToken);
        var deletedCards = entries.Select(x => x.CardId).Distinct().Count();
        db.UserTcgCards.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken);
        return new DeleteTcgCardsResultDto(cardIds.Count, deletedCards, entries.Count);
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
        var setProgress = entries.GroupBy(x => new { x.Card.Set.Provider, x.Card.Set.ProviderSetId })
            .Select(group => new TcgSetProgressDto(
                group.Select(x => x.Card.SetId).First(),
                group.Key.ProviderSetId,
                group.Select(x => x.Card.Set).First().Name,
                group.Select(x => x.CardId).Distinct().Count(),
                group.Select(x => x.Card.Set).First().Total,
                Percent(group.Select(x => x.CardId).Distinct().Count(), group.Select(x => x.Card.Set).First().Total)))
            .GroupBy(x => x.ProviderSetId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
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
        var metadataMissing = await db.TcgSets.AsNoTracking().AnyAsync(
            x => x.SeriesId == null || x.OfficialCode == null,
            cancellationToken);
        if (newest.HasValue && newest.Value > DateTime.UtcNow.AddDays(-7) && !metadataMissing) return;
        await SetsSyncLock.WaitAsync(cancellationToken);
        try
        {
            newest = await db.TcgSets.AsNoTracking().MaxAsync(x => (DateTime?)x.SyncedAt, cancellationToken);
            metadataMissing = await db.TcgSets.AsNoTracking().AnyAsync(
                x => x.SeriesId == null || x.OfficialCode == null,
                cancellationToken);
            if (newest.HasValue && newest.Value > DateTime.UtcNow.AddDays(-7) && !metadataMissing) return;
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
        var spanishById = spanish.Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var all = english.Concat(spanish)
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.SetId))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        if (all.Count == 0) return;

        var providerSetIds = all.Select(x => x.SetId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sets = await db.TcgSets
            .Where(x => x.Provider == "tcgdex" && providerSetIds.Contains(x.ProviderSetId))
            .ToListAsync(cancellationToken);
        var setsById = sets.ToDictionary(x => x.ProviderSetId, StringComparer.OrdinalIgnoreCase);
        foreach (var providerCard in all)
        {
            if (setsById.ContainsKey(providerCard.SetId)) continue;
            var set = new TcgSetEntity
            {
                Provider = "tcgdex",
                ProviderSetId = providerCard.SetId,
                Name = string.IsNullOrWhiteSpace(providerCard.SetName) ? providerCard.SetId : providerCard.SetName
            };
            setsById[providerCard.SetId] = set;
            db.TcgSets.Add(set);
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);

        var providerCardIds = all.Select(x => x.Id).ToList();
        var cards = await db.TcgCards
            .Where(x => x.Provider == "tcgdex" && providerCardIds.Contains(x.ProviderCardId))
            .ToListAsync(cancellationToken);
        var cardsById = cards.ToDictionary(x => x.ProviderCardId, StringComparer.OrdinalIgnoreCase);
        var newProviderCardIds = new List<string>();
        foreach (var providerCard in all)
        {
            var set = setsById[providerCard.SetId];
            if (!cardsById.TryGetValue(providerCard.Id, out var entity))
            {
                entity = new TcgCardEntity
                {
                    ProviderCardId = providerCard.Id,
                    SetId = set.Id,
                    Name = providerCard.Name,
                    Number = providerCard.Number
                };
                cardsById[providerCard.Id] = entity;
                db.TcgCards.Add(entity);
                newProviderCardIds.Add(providerCard.Id);
            }
            ApplyCard(entity, providerCard, spanishById.GetValueOrDefault(providerCard.Id));
            entity.SetId = set.Id;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (newProviderCardIds.Count > 0)
        {
            var newCardIds = await db.TcgCards.AsNoTracking()
                .Where(x => x.Provider == "tcgdex" && newProviderCardIds.Contains(x.ProviderCardId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            await assets.CacheCardsAsync(newCardIds, cancellationToken);
        }
    }

    private async Task<string?> RefreshCardEntityAsync(int userId, TcgCardEntity entity, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        TcgProviderCard? english = null;
        TcgProviderCard? spanish = null;
        try { english = await tcgDex.GetCardAsync(entity.ProviderCardId, "en", cancellationToken); }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            errors.Add(ProviderError("TCGdex English", exception));
        }
        if (english is null)
        {
            try { spanish = await tcgDex.GetCardAsync(entity.ProviderCardId, "es", cancellationToken); }
            catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
            {
                errors.Add(ProviderError("TCGdex Spanish", exception));
            }
        }

        var updated = english is not null || spanish is not null;
        if (updated) ApplyCard(entity, english ?? spanish!, spanish);
        else errors.Add("TCGdex did not return card detail.");

        var apiKey = await credentials.GetTcgApiKeyAsync(userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(apiKey) &&
            (entity.PriceEur is null || entity.PriceUsd is null ||
             entity.PriceCheckedAt is null || entity.PriceCheckedAt < DateTime.UtcNow.AddHours(-24)))
        {
            try
            {
                var enriched = await pokemonTcgIo.GetCardAsync(entity.PokemonTcgIoId ?? entity.ProviderCardId, apiKey, cancellationToken);
                if (enriched is not null)
                {
                    ApplyEnrichment(entity, enriched);
                    updated = true;
                }
                else
                {
                    errors.Add("Pokemon TCG API did not return card detail.");
                }
            }
            catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
            {
                errors.Add(ProviderError("Pokemon TCG API", exception));
            }
        }

        entity.PriceCheckedAt = DateTime.UtcNow;
        var refreshError = string.Join(" ", errors.Distinct());
        entity.LastRefreshError = updated
            ? null
            : refreshError.Length <= 1000 ? refreshError : refreshError[..1000];
        await db.SaveChangesAsync(cancellationToken);
        return entity.LastRefreshError;
    }

    private async Task<TcgCardPageDto> QuerySearchCardsAsync(
        int userId,
        string? nameQuery,
        int? setId,
        string? textNumber,
        CollectorReference? collector,
        int? speciesId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var source = db.TcgCards.AsNoTracking().Include(x => x.Set).AsQueryable();
        if (setId.HasValue) source = source.Where(x => x.SetId == setId.Value);
        if (!string.IsNullOrWhiteSpace(nameQuery))
        {
            var term = nameQuery.ToLowerInvariant();
            source = source.Where(x => x.Name.ToLower().Contains(term) ||
                (x.NameEn != null && x.NameEn.ToLower().Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(textNumber))
        {
            var normalizedNumber = textNumber.ToLowerInvariant();
            source = source.Where(x => x.Number.ToLower() == normalizedNumber);
        }
        if (speciesId.HasValue)
        {
            source = source.Where(card =>
                card.NationalPokedexNumbersJson == $"[{speciesId.Value}]" ||
                card.NationalPokedexNumbersJson.StartsWith($"[{speciesId.Value},") ||
                card.NationalPokedexNumbersJson.EndsWith($",{speciesId.Value}]") ||
                card.NationalPokedexNumbersJson.Contains($",{speciesId.Value},"));
        }

        if (collector is null)
        {
            var total = await source.CountAsync(cancellationToken);
            var cards = await source.OrderByDescending(x => x.Set.ReleaseDate).ThenBy(x => x.Number)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            var owned = await GetOwnedLookupAsync(userId, cards.Select(x => x.Id), cancellationToken);
            return new TcgCardPageDto(
                cards.Select(x => ToCardDto(x, owned.GetValueOrDefault(x.Id) ?? [])).ToList(),
                page,
                pageSize,
                page * pageSize < total,
                total);
        }

        var numericText = collector.NumericValue.ToString();
        source = source.Where(x => x.Number.Contains(numericText));
        if (collector.PrintedTotal.HasValue)
            source = source.Where(x => x.Set.PrintedTotal == collector.PrintedTotal.Value);
        var candidates = await source.OrderByDescending(x => x.Set.ReleaseDate).ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);
        candidates = candidates.Where(x => CollectorNumbersEqual(x.Number, collector.LocalId)).ToList();
        var exactTotal = candidates.Count;
        var cardsPage = candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ownedPage = await GetOwnedLookupAsync(userId, cardsPage.Select(x => x.Id), cancellationToken);
        return new TcgCardPageDto(
            cardsPage.Select(x => ToCardDto(x, ownedPage.GetValueOrDefault(x.Id) ?? [])).ToList(),
            page,
            pageSize,
            page * pageSize < exactTotal,
            exactTotal);
    }

    private async Task<TcgSetEntity?> ResolveOfficialSetAsync(
        string officialCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = officialCode.Trim().ToUpperInvariant();
        var local = await db.TcgSets.SingleOrDefaultAsync(
            x => x.OfficialCode != null && x.OfficialCode.ToUpper() == normalizedCode,
            cancellationToken);
        if (local is not null) return local;

        var english = await tcgDex.GetSetByOfficialCodeAsync(normalizedCode, "en", cancellationToken);
        if (english is null) return null;
        TcgProviderSet? spanish = null;
        try { spanish = await tcgDex.GetSetByOfficialCodeAsync(normalizedCode, "es", cancellationToken); }
        catch (HttpRequestException) { }

        await SetsSyncLock.WaitAsync(cancellationToken);
        try
        {
            local = await db.TcgSets.SingleOrDefaultAsync(
                x => x.Provider == "tcgdex" && x.ProviderSetId == english.Id,
                cancellationToken);
            local ??= new TcgSetEntity { ProviderSetId = english.Id };
            if (db.Entry(local).State == EntityState.Detached) db.TcgSets.Add(local);
            ApplySet(local, english, spanish?.Id.Equals(english.Id, StringComparison.OrdinalIgnoreCase) == true ? spanish : null);
            await db.SaveChangesAsync(cancellationToken);
            return local;
        }
        finally
        {
            SetsSyncLock.Release();
        }
    }

    private async Task<IReadOnlyList<TcgProviderCard>> PostFilterProviderCardsAsync(
        IReadOnlyList<TcgProviderCard> cards,
        string? nameQuery,
        TcgSetEntity? selectedSet,
        string? textNumber,
        CollectorReference? collector,
        int? speciesId,
        CancellationToken cancellationToken)
    {
        if (cards.Count == 0) return [];
        var requestedPrintedTotal = collector?.PrintedTotal;
        var printedTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (requestedPrintedTotal.HasValue && selectedSet is null)
        {
            var setIds = cards.Select(x => x.SetId).Distinct().ToList();
            var totals = await db.TcgSets.AsNoTracking()
                .Where(x => setIds.Contains(x.ProviderSetId))
                .Select(x => new { x.ProviderSetId, x.PrintedTotal })
                .ToListAsync(cancellationToken);
            printedTotals = totals.ToDictionary(x => x.ProviderSetId, x => x.PrintedTotal, StringComparer.OrdinalIgnoreCase);
        }

        return cards.Where(card =>
                (selectedSet is null || card.SetId.Equals(selectedSet.ProviderSetId, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(nameQuery) ||
                    card.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(textNumber) ||
                    card.Number.Equals(textNumber, StringComparison.OrdinalIgnoreCase)) &&
                (collector is null || CollectorNumbersEqual(card.Number, collector.LocalId)) &&
                (!requestedPrintedTotal.HasValue || selectedSet is not null ||
                    printedTotals.GetValueOrDefault(card.SetId) == requestedPrintedTotal.Value) &&
                (!speciesId.HasValue || card.NationalPokedexNumbers.Count == 0 ||
                    card.NationalPokedexNumbers.Contains(speciesId.Value)))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static IReadOnlyList<TcgProviderCard> AddMissingSpecies(IReadOnlyList<TcgProviderCard> cards, int speciesId) =>
        cards.Select(card => card.NationalPokedexNumbers.Count == 0
            ? card with { NationalPokedexNumbers = [speciesId] }
            : card).ToList();

    private static bool TryParseCollectorReference(string? value, out CollectorReference reference)
    {
        reference = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var match = CollectorReferencePattern.Match(value);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var numericValue)) return false;
        int? printedTotal = null;
        if (match.Groups["total"].Success)
        {
            if (!int.TryParse(match.Groups["total"].Value, out var parsedTotal) || parsedTotal <= 0) return false;
            printedTotal = parsedTotal;
        }

        var code = match.Groups["code"].Success ? match.Groups["code"].Value.ToUpperInvariant() : null;
        reference = new CollectorReference(code, match.Groups["number"].Value, numericValue, printedTotal);
        return true;
    }

    private static bool CollectorNumbersEqual(string first, string second)
    {
        var firstDigits = GetCollectorNumerator(first);
        var secondDigits = GetCollectorNumerator(second);
        if (firstDigits.Length == 0 || secondDigits.Length == 0 ||
            firstDigits.Any(x => !char.IsDigit(x)) || secondDigits.Any(x => !char.IsDigit(x)))
        {
            return firstDigits.Equals(secondDigits, StringComparison.OrdinalIgnoreCase);
        }

        return NormalizeCollectorDigits(firstDigits) == NormalizeCollectorDigits(secondDigits);
    }

    private static string GetCollectorNumerator(string value) => value.Split('/', 2)[0].Trim();

    private static string NormalizeCollectorDigits(string value)
    {
        var normalized = value.TrimStart('0');
        return normalized.Length == 0 ? "0" : normalized;
    }

    private static TcgCardPageDto EmptyCardPage(int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        return new TcgCardPageDto([], page, pageSize, false, 0);
    }

    private static bool IsProviderFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    private static string ProviderError(string provider, Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message) ? "request failed" : exception.Message.Trim();
        return $"{provider}: {message}";
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

    private static TcgCardDto ToCardDto(TcgCardEntity card, IReadOnlyList<UserTcgCardEntity> owned)
    {
        var collectorReference = GetCollectorReference(card);
        var cardmarketUrl = BuildMarketplaceSearchUrl("cardmarket", collectorReference) ?? card.CardmarketUrl;
        var tcgplayerUrl = BuildMarketplaceSearchUrl("tcgplayer", collectorReference) ?? card.TcgplayerUrl;
        return new(
        card.Id, card.ProviderCardId, card.Name, card.NameEn, card.Number, card.Rarity, card.Artist,
        GetCardAssetUrl(card, "small"), GetCardAssetUrl(card, "large"), Deserialize<int>(card.NationalPokedexNumbersJson),
        Deserialize<string>(card.VariantsJson), card.SetId, card.Set.ProviderSetId, card.Set.Name, collectorReference,
        new TcgPriceDto(
            card.PriceEur,
            card.PriceUsd,
            card.PriceUpdatedAt,
            cardmarketUrl,
            tcgplayerUrl,
            DeserializeDictionary(card.VariantPricesEurJson),
            DeserializeDictionary(card.VariantPricesUsdJson)),
        owned.Select(x => new TcgOwnedEntryDto(x.Id, x.Variant, x.Condition, x.Language, x.Quantity, x.Notes)).ToList(),
        owned.Sum(x => x.Quantity),
        card.DetailedAt,
        card.PriceCheckedAt,
        card.LastRefreshError);
    }

    private static UserCardDto ToUserCardDto(UserTcgCardEntity entry, TcgCardDto card)
    {
        var entity = entry.Card;
        var eur = GetVariantPrice(entity.VariantPricesEurJson, entry.Variant, entity.PriceEur);
        var usd = GetVariantPrice(entity.VariantPricesUsdJson, entry.Variant, entity.PriceUsd);
        return new UserCardDto(entry.Id, card, entry.Variant, entry.Condition, entry.Language, entry.Quantity,
            entry.Notes, entry.AddedAt, eur, usd, eur * entry.Quantity, usd * entry.Quantity);
    }

    private static TcgCollectionEntryDto ToCollectionEntryDto(UserTcgCardEntity entry)
    {
        var eur = GetVariantPrice(entry.Card.VariantPricesEurJson, entry.Variant, entry.Card.PriceEur);
        var usd = GetVariantPrice(entry.Card.VariantPricesUsdJson, entry.Variant, entry.Card.PriceUsd);
        return new TcgCollectionEntryDto(
            entry.Id,
            entry.Variant,
            entry.Condition,
            entry.Language,
            entry.Quantity,
            entry.Notes,
            entry.AddedAt,
            entry.UpdatedAt,
            eur,
            usd,
            eur * entry.Quantity,
            usd * entry.Quantity);
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
        entity.Name = FirstNotEmpty(spanish?.Name, english.Name, entity.Name) ?? english.Id;
        entity.NameEn = FirstNotEmpty(english.Name, entity.NameEn);
        entity.Series = FirstNotEmpty(spanish?.Series, english.Series, entity.Series);
        entity.SeriesId = FirstNotEmpty(spanish?.SeriesId, english.SeriesId, entity.SeriesId);
        entity.OfficialCode = FirstNotEmpty(spanish?.OfficialCode, english.OfficialCode, entity.OfficialCode);
        if (english.PrintedTotal > 0) entity.PrintedTotal = english.PrintedTotal;
        if (english.Total > 0) entity.Total = english.Total;
        entity.ReleaseDate = english.ReleaseDate ?? entity.ReleaseDate;
        entity.SymbolUrl = FirstNotEmpty(spanish?.SymbolUrl, english.SymbolUrl, entity.SymbolUrl);
        entity.LogoUrl = FirstNotEmpty(spanish?.LogoUrl, english.LogoUrl, entity.LogoUrl);
        entity.SyncedAt = DateTime.UtcNow;
    }

    private static void ApplyCard(TcgCardEntity entity, TcgProviderCard english, TcgProviderCard? spanish)
    {
        var localized = spanish ?? english;
        entity.Name = FirstNotEmpty(localized.Name, english.Name, entity.Name) ?? entity.ProviderCardId;
        entity.NameEn = FirstNotEmpty(english.Name, entity.NameEn);
        entity.Number = FirstNotEmpty(localized.Number, english.Number, entity.Number) ?? string.Empty;
        entity.Rarity = FirstNotEmpty(localized.Rarity, english.Rarity, entity.Rarity);
        entity.Artist = FirstNotEmpty(localized.Artist, english.Artist, entity.Artist);
        entity.ImageSmall = FirstNotEmpty(localized.ImageSmall, english.ImageSmall, entity.ImageSmall);
        entity.ImageLarge = FirstNotEmpty(localized.ImageLarge, english.ImageLarge, entity.ImageLarge);

        var dexIds = Deserialize<int>(entity.NationalPokedexNumbersJson)
            .Concat(english.NationalPokedexNumbers)
            .Concat(localized.NationalPokedexNumbers)
            .Distinct()
            .ToList();
        if (dexIds.Count > 0) entity.NationalPokedexNumbersJson = JsonSerializer.Serialize(dexIds, JsonOptions);

        var variants = Deserialize<string>(entity.VariantsJson)
            .Concat(english.Variants)
            .Concat(localized.Variants)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (variants.Count > 0) entity.VariantsJson = JsonSerializer.Serialize(variants, JsonOptions);

        entity.PriceEur = localized.PriceEur ?? english.PriceEur ?? entity.PriceEur;
        entity.PriceUsd = localized.PriceUsd ?? english.PriceUsd ?? entity.PriceUsd;
        entity.VariantPricesEurJson = MergePriceJson(
            entity.VariantPricesEurJson,
            english.VariantPricesEur,
            localized.VariantPricesEur);
        entity.VariantPricesUsdJson = MergePriceJson(
            entity.VariantPricesUsdJson,
            english.VariantPricesUsd,
            localized.VariantPricesUsd);
        entity.PriceUpdatedAt = Latest(entity.PriceUpdatedAt, Latest(english.PriceUpdatedAt, localized.PriceUpdatedAt));
        entity.CardmarketUrl = FirstNotEmpty(localized.CardmarketUrl, english.CardmarketUrl, entity.CardmarketUrl);
        entity.TcgplayerUrl = FirstNotEmpty(localized.TcgplayerUrl, english.TcgplayerUrl, entity.TcgplayerUrl);
        entity.ProviderMetadataJson = FirstNotEmpty(english.RawMetadataJson, localized.RawMetadataJson, entity.ProviderMetadataJson) ?? "{}";
        var collectorReference = GetCollectorReference(entity);
        entity.CardmarketUrl ??= BuildMarketplaceSearchUrl("cardmarket", collectorReference)
            ?? $"https://www.cardmarket.com/en/Pokemon/Products/Search?searchString={Uri.EscapeDataString(localized.Name)}";
        entity.TcgplayerUrl ??= BuildMarketplaceSearchUrl("tcgplayer", collectorReference)
            ?? $"https://www.tcgplayer.com/search/pokemon/product?productLineName=pokemon&q={Uri.EscapeDataString(localized.Name)}";
        entity.SyncedAt = DateTime.UtcNow;
        if (english.IsComplete || localized.IsComplete) entity.DetailedAt = DateTime.UtcNow;
    }

    private static void ApplyEnrichment(TcgCardEntity entity, TcgProviderCard value)
    {
        entity.PokemonTcgIoId = value.Id;
        entity.PriceEur = value.PriceEur ?? entity.PriceEur;
        entity.PriceUsd = value.PriceUsd ?? entity.PriceUsd;
        entity.ImageSmall ??= value.ImageSmall;
        entity.ImageLarge ??= value.ImageLarge;
        entity.VariantPricesEurJson = MergePriceJson(entity.VariantPricesEurJson, value.VariantPricesEur);
        entity.VariantPricesUsdJson = MergePriceJson(entity.VariantPricesUsdJson, value.VariantPricesUsd);
        entity.PriceUpdatedAt = Latest(entity.PriceUpdatedAt, value.PriceUpdatedAt);
        entity.CardmarketUrl = value.CardmarketUrl ?? entity.CardmarketUrl;
        entity.TcgplayerUrl = value.TcgplayerUrl ?? entity.TcgplayerUrl;
        entity.ProviderMetadataJson = value.RawMetadataJson ?? entity.ProviderMetadataJson;
        entity.VariantsJson = JsonSerializer.Serialize(
            Deserialize<string>(entity.VariantsJson).Concat(value.Variants).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            JsonOptions);
        entity.DetailedAt = DateTime.UtcNow;
    }

    private static TcgSetDto ToSetDto(TcgSetEntity set, int unique, int copies) => new(
        set.Id, set.ProviderSetId, set.Name, set.NameEn, set.Series, set.SeriesId, set.OfficialCode,
        set.PrintedTotal, set.Total, set.ReleaseDate,
        string.IsNullOrWhiteSpace(set.SymbolUrl) ? null : $"/tcg/assets/sets/{set.Id}/symbol",
        string.IsNullOrWhiteSpace(set.LogoUrl) ? null : $"/tcg/assets/sets/{set.Id}/logo",
        unique, copies, Percent(unique, set.Total));

    private static string? GetCardAssetUrl(TcgCardEntity card, string size)
    {
        var hasSource = !string.IsNullOrWhiteSpace(card.ImageSmall) || !string.IsNullOrWhiteSpace(card.ImageLarge);
        var canInfer = card.Provider.Equals("tcgdex", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(card.Set.ProviderSetId) &&
            !string.IsNullOrWhiteSpace(card.Number);
        return hasSource || canInfer
            ? $"/tcg/assets/cards/{card.Id}/{size}"
            : null;
    }

    private static string MergePriceJson(string existingJson, params IReadOnlyDictionary<string, decimal>[] incoming)
    {
        var merged = DeserializeDictionary(existingJson);
        foreach (var prices in incoming)
            foreach (var price in prices)
                merged[TcgDexProvider.NormalizeVariant(price.Key)] = price.Value;
        return JsonSerializer.Serialize(merged, JsonOptions);
    }

    private static DateTime? Latest(DateTime? first, DateTime? second)
    {
        if (!first.HasValue) return second;
        if (!second.HasValue) return first;
        return first.Value >= second.Value ? first : second;
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? GetCollectorReference(TcgCardEntity card)
    {
        var setCode = FirstNotEmpty(card.Set.OfficialCode, card.Set.ProviderSetId);
        var number = card.Number.Trim();
        return string.IsNullOrWhiteSpace(setCode) || number.Length == 0
            ? null
            : $"{setCode.Trim().ToUpperInvariant()} {number}";
    }

    private static string? BuildMarketplaceSearchUrl(string provider, string? collectorReference)
    {
        if (string.IsNullOrWhiteSpace(collectorReference)) return null;
        var encodedReference = Uri.EscapeDataString(collectorReference);
        return provider switch
        {
            "cardmarket" => $"https://www.cardmarket.com/en/Pokemon/Products/Search?searchString={encodedReference}",
            "tcgplayer" => $"https://www.tcgplayer.com/search/pokemon/product?productLineName=pokemon&q={encodedReference}",
            _ => null
        };
    }

    private static decimal? GetVariantPrice(string json, string variant, decimal? fallback)
    {
        var prices = DeserializeDictionary(json);
        var normalized = TcgDexProvider.NormalizeVariant(variant);
        if (prices.TryGetValue(normalized, out var exact)) return exact;
        if (prices.TryGetValue(variant, out exact)) return exact;
        return prices.Count == 0 ? fallback : null;
    }

    private static Dictionary<string, decimal> DeserializeDictionary(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions) ?? [];
            var normalized = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
                normalized[TcgDexProvider.NormalizeVariant(value.Key)] = value.Value;
            return normalized;
        }
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

    private sealed record CollectorReference(
        string? OfficialCode,
        string LocalId,
        int NumericValue,
        int? PrintedTotal);
}
