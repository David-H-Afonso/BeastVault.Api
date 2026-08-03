namespace BeastVault.Api.Contracts;

public sealed record TcgPriceDto(
    decimal? Eur,
    decimal? Usd,
    DateTime? UpdatedAt,
    string? CardmarketUrl,
    string? TcgplayerUrl,
    IReadOnlyDictionary<string, decimal> VariantEur,
    IReadOnlyDictionary<string, decimal> VariantUsd);

public sealed record TcgOwnedEntryDto(
    int Id,
    string Variant,
    string Condition,
    string Language,
    int Quantity,
    string? Notes);

public sealed record TcgCardDto(
    int Id,
    string ProviderCardId,
    string Name,
    string? NameEn,
    string Number,
    string? Rarity,
    string? Artist,
    string? ImageSmall,
    string? ImageLarge,
    IReadOnlyList<int> NationalPokedexNumbers,
    IReadOnlyList<string> Variants,
    int SetId,
    string SetProviderId,
    string SetName,
    string? CollectorReference,
    TcgPriceDto Prices,
    IReadOnlyList<TcgOwnedEntryDto> Owned,
    int TotalOwned,
    DateTime? DetailedAt,
    DateTime? PriceCheckedAt,
    string? LastRefreshError);

public sealed record TcgCardPageDto(
    IReadOnlyList<TcgCardDto> Items,
    int Page,
    int PageSize,
    bool HasMore,
    int? TotalCount);

public sealed record TcgSetDto(
    int Id,
    string ProviderSetId,
    string Name,
    string? NameEn,
    string? Series,
    string? SeriesId,
    string? OfficialCode,
    int PrintedTotal,
    int Total,
    DateTime? ReleaseDate,
    string? SymbolUrl,
    string? LogoUrl,
    int OwnedUniqueCards,
    int OwnedCopies,
    decimal CompletionPercent);

public sealed record UserCardDto(
    int Id,
    TcgCardDto Card,
    string Variant,
    string Condition,
    string Language,
    int Quantity,
    string? Notes,
    DateTime AddedAt,
    decimal? UnitValueEur,
    decimal? UnitValueUsd,
    decimal? TotalValueEur,
    decimal? TotalValueUsd);

public sealed record TcgCollectionPageDto(
    IReadOnlyList<TcgCollectionCardDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TcgCollectionEntryDto(
    int Id,
    string Variant,
    string Condition,
    string Language,
    int Quantity,
    string? Notes,
    DateTime AddedAt,
    DateTime UpdatedAt,
    decimal? UnitValueEur,
    decimal? UnitValueUsd,
    decimal? TotalValueEur,
    decimal? TotalValueUsd);

public sealed record TcgCollectionCardDto(
    TcgCardDto Card,
    IReadOnlyList<TcgCollectionEntryDto> Entries,
    int TotalCopies,
    decimal TotalValueEur,
    decimal TotalValueUsd,
    DateTime UpdatedAt);

public sealed record TcgMissingSpeciesDto(int SpeciesId, string SpeciesName);

public sealed record TcgDexProgressDto(
    string Name,
    int Owned,
    int Total,
    decimal CompletionPercent,
    IReadOnlyList<TcgMissingSpeciesDto> Missing);

public sealed record TcgSetProgressDto(
    int SetId,
    string ProviderSetId,
    string Name,
    int Owned,
    int Total,
    decimal CompletionPercent);

public sealed record TcgCollectionStatsDto(
    int UniqueCards,
    int TotalCopies,
    decimal TotalValueEur,
    decimal TotalValueUsd,
    TcgDexProgressDto National,
    IReadOnlyList<TcgDexProgressDto> Regions,
    IReadOnlyList<TcgSetProgressDto> Sets,
    IReadOnlyList<UserCardDto> TopCards);

public sealed record AddTcgCollectionEntryRequest(
    int CardId,
    string Variant,
    string Condition,
    string Language,
    int Quantity,
    string? Notes);

public sealed record TcgBulkResolveRequest(IReadOnlyList<string>? Identifiers);

public sealed record TcgBulkResolveItemDto(
    int Index,
    string Input,
    int Quantity,
    bool Success,
    string? Error,
    TcgCardDto? Card);

public sealed record TcgBulkResolveResultDto(
    IReadOnlyList<TcgBulkResolveItemDto> Items,
    int Requested,
    int Resolved,
    int Failed,
    bool Truncated);

public sealed record AddTcgCollectionBulkItemRequest(
    int Index,
    int CardId,
    string Variant,
    string Condition,
    string Language,
    int Quantity,
    string? Notes);

public sealed record AddTcgCollectionBulkRequest(IReadOnlyList<AddTcgCollectionBulkItemRequest>? Items);

public sealed record TcgBulkAddItemDto(
    int Index,
    int CardId,
    bool Success,
    string? Error,
    UserCardDto? Entry);

public sealed record TcgBulkAddResultDto(
    IReadOnlyList<TcgBulkAddItemDto> Items,
    int Requested,
    int Added,
    int Failed);

public sealed record UpdateTcgCollectionEntryRequest(
    string? Variant = null,
    string? Condition = null,
    string? Language = null,
    int? Quantity = null,
    string? Notes = null);

public sealed record DeleteTcgCardsRequest(IReadOnlyList<int>? CardIds);

public sealed record DeleteTcgCardsResultDto(
    int RequestedCards,
    int DeletedCards,
    int DeletedEntries);

public sealed record TcgBatchRefreshRequest(
    IReadOnlyList<int>? CardIds = null,
    bool OwnedOnly = false);

public sealed record TcgCardRefreshResultDto(
    int CardId,
    bool Success,
    string? Error,
    TcgCardDto? Card);

public sealed record TcgBatchRefreshResultDto(
    IReadOnlyList<TcgCardRefreshResultDto> Items,
    int Requested,
    int Processed,
    bool Truncated);

public sealed record TcgAssetCacheResultDto(int Requested, int Cached);

public sealed record TcgApiKeyStatusDto(
    bool Configured,
    string? MaskedApiKey,
    DateTime? UpdatedAt);

public sealed record UpdateTcgApiKeyRequest(string? ApiKey);

public sealed record TcgSyncResultDto(int Sets, int Cards, int Errors, bool IncludedCards);
