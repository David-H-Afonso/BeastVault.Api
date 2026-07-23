using BeastVault.Api.Contracts;

namespace BeastVault.Api.Application.Interfaces;

public interface IHouseholdIntegrationService
{
    Task<HouseholdServiceResult<HouseholdAuthorizeResponse>> AuthorizeAsync(
        int userId,
        HouseholdAuthorizeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdServiceResult<HouseholdTokenResponse>> TokenAsync(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        HouseholdRevokeRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMeResponse?> GetMeAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);
}

public sealed record HouseholdServiceResult<T>(T? Value, string? Error, string? ErrorDescription)
{
    public bool Succeeded => Error is null && Value is not null;

    public static HouseholdServiceResult<T> Success(T value) => new(value, null, null);
    public static HouseholdServiceResult<T> Fail(string error, string description) =>
        new(default, error, description);
}
