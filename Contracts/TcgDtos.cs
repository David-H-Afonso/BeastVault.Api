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
    TcgPriceDto Prices,
    IReadOnlyList<TcgOwnedEntryDto> Owned,
    int TotalOwned);

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
    IReadOnlyList<UserCardDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

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

public sealed record UpdateTcgCollectionEntryRequest(
    string? Variant = null,
    string? Condition = null,
    string? Language = null,
    int? Quantity = null,
    string? Notes = null);

public sealed record TcgApiKeyStatusDto(
    bool Configured,
    string? MaskedApiKey,
    DateTime? UpdatedAt);

public sealed record UpdateTcgApiKeyRequest(string? ApiKey);

public sealed record TcgSyncResultDto(int Sets, int Cards, int Errors, bool IncludedCards);
