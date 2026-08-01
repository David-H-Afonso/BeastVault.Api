using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public sealed class UserApiCredentialService : IUserApiCredentialService
{
    private const string Provider = "pokemontcg.io";
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<UserApiCredentialService> _logger;

    public UserApiCredentialService(
        AppDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<UserApiCredentialService> logger)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("BeastVault.TcgApiKey.v1");
        _logger = logger;
    }

    public async Task<TcgApiKeyStatusDto> GetTcgApiKeyStatusAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var credential = await _db.UserApiCredentials.AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == userId && x.Provider == Provider,
            cancellationToken);
        if (credential is null) return new TcgApiKeyStatusDto(false, null, null);
        try
        {
            _protector.Unprotect(credential.ProtectedValue);
            return new TcgApiKeyStatusDto(true, $"••••{credential.LastFour}", credential.UpdatedAt);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning("The stored TCG credential for user {UserId} cannot be decrypted.", userId);
            return new TcgApiKeyStatusDto(false, null, credential.UpdatedAt);
        }
    }

    public async Task<string?> GetTcgApiKeyAsync(int userId, CancellationToken cancellationToken)
    {
        var protectedValue = await _db.UserApiCredentials.AsNoTracking()
            .Where(x => x.UserId == userId && x.Provider == Provider)
            .Select(x => x.ProtectedValue)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(protectedValue)) return null;

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning("The stored TCG credential for user {UserId} cannot be decrypted.", userId);
            return null;
        }
    }

    public async Task<TcgApiKeyStatusDto> SetTcgApiKeyAsync(
        int userId,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var credential = await _db.UserApiCredentials.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Provider == Provider,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (credential is not null)
            {
                _db.UserApiCredentials.Remove(credential);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return new TcgApiKeyStatusDto(false, null, null);
        }

        var normalized = apiKey.Trim();
        if (normalized.Length is < 8 or > 512)
            throw new ArgumentException("The Pokémon TCG API key is invalid.");

        credential ??= new UserApiCredentialEntity { UserId = userId, Provider = Provider };
        credential.ProtectedValue = _protector.Protect(normalized);
        credential.LastFour = normalized[^Math.Min(4, normalized.Length)..];
        credential.UpdatedAt = DateTime.UtcNow;
        if (_db.Entry(credential).State == EntityState.Detached)
            _db.UserApiCredentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);

        return new TcgApiKeyStatusDto(true, $"••••{credential.LastFour}", credential.UpdatedAt);
    }
}
