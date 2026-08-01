using BeastVault.Api.Contracts;

namespace BeastVault.Api.Application.Interfaces;

public sealed record TcgProviderSet(
    string Id,
    string Name,
    string? Series,
    int PrintedTotal,
    int Total,
    DateTime? ReleaseDate,
    string? SymbolUrl,
    string? LogoUrl,
    IReadOnlyList<TcgProviderCard> Cards);

public sealed record TcgProviderCard(
    string Id,
    string SetId,
    string SetName,
    string Name,
    string Number,
    string? Rarity,
    string? Artist,
    string? ImageSmall,
    string? ImageLarge,
    IReadOnlyList<int> NationalPokedexNumbers,
    IReadOnlyList<string> Variants,
    decimal? PriceEur,
    decimal? PriceUsd,
    IReadOnlyDictionary<string, decimal> VariantPricesEur,
    IReadOnlyDictionary<string, decimal> VariantPricesUsd,
    DateTime? PriceUpdatedAt,
    string? CardmarketUrl,
    string? TcgplayerUrl);

public interface ITcgDexProvider
{
    Task<IReadOnlyList<TcgProviderSet>> GetSetsAsync(string language, CancellationToken cancellationToken);
    Task<TcgProviderSet?> GetSetAsync(string setId, string language, CancellationToken cancellationToken);
    Task<TcgProviderCard?> GetCardAsync(string cardId, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<TcgProviderCard>> SearchCardsAsync(
        string? query,
        string? setId,
        string? number,
        int? speciesId,
        int page,
        int pageSize,
        string language,
        CancellationToken cancellationToken);
}

public interface IPokemonTcgIoProvider
{
    Task<TcgProviderCard?> GetCardAsync(string cardId, string apiKey, CancellationToken cancellationToken);
}

public interface IUserApiCredentialService
{
    Task<TcgApiKeyStatusDto> GetTcgApiKeyStatusAsync(int userId, CancellationToken cancellationToken);
    Task<string?> GetTcgApiKeyAsync(int userId, CancellationToken cancellationToken);
    Task<TcgApiKeyStatusDto> SetTcgApiKeyAsync(int userId, string? apiKey, CancellationToken cancellationToken);
}
