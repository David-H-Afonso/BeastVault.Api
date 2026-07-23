namespace BeastVault.Api.Contracts;

public sealed record HouseholdAuthorizeRequest(
    string ClientId,
    string RedirectUri,
    string State,
    string CodeChallenge,
    string CodeChallengeMethod,
    IReadOnlyList<string> Scopes,
    bool Approved = true);

public sealed record HouseholdAuthorizeResponse(string RedirectUri);

public sealed record HouseholdTokenRequest(
    string GrantType,
    string ClientId,
    string? RedirectUri,
    string? Code,
    string? CodeVerifier,
    string? RefreshToken);

public sealed record HouseholdRevokeRequest(string Token, string? TokenTypeHint);

public sealed record HouseholdAccountDto(string Id, string DisplayName);

public sealed record HouseholdTokenResponse(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    int RefreshExpiresIn,
    string Scope,
    Guid ConnectionId,
    HouseholdAccountDto Account);

public sealed record HouseholdMeResponse(
    Guid ConnectionId,
    HouseholdAccountDto Account,
    IReadOnlyList<string> Scopes);

public sealed record HouseholdFavoriteRequest(bool Favorite);
public sealed record HouseholdNotesRequest(string? Notes);

public sealed record HouseholdPokemonSummaryDto(PokemonSummaryCountsDto Counts);

public sealed record HouseholdPokemonTagDto(
    int Id,
    string Name,
    string? ImagePath,
    string? ColorHex);

public sealed record HouseholdTagFilterDto(
    int Id,
    string Name,
    string? ImagePath,
    int PokemonCount,
    string Category,
    string? ColorHex);

public sealed record HouseholdPokemonListItemDto(
    int Id,
    int SpeciesId,
    string SpeciesName,
    string? Nickname,
    int Level,
    bool IsShiny,
    bool Favorite,
    bool IsEgg,
    string? Type1,
    string? Type2,
    string SpriteUrl,
    IReadOnlyList<HouseholdPokemonTagDto> Tags);

public sealed record HouseholdPokemonListResponseDto(
    IReadOnlyList<HouseholdPokemonListItemDto> Items,
    int Total);

public sealed record HouseholdPokemonDetailDto(
    int Id,
    int SpeciesId,
    string SpeciesName,
    int Form,
    string FormName,
    string? Nickname,
    int Level,
    bool IsShiny,
    bool IsEgg,
    bool Favorite,
    string? Notes,
    string NatureName,
    string AbilityName,
    string BallName,
    string GenderName,
    string OriginGameName,
    int MetLevel);
