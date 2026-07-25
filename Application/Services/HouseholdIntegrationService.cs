using System.Security.Cryptography;
using System.Text;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Configuration;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeastVault.Api.Application.Services;

public sealed class HouseholdIntegrationService : IHouseholdIntegrationService
{
    private const string AccessTokenPrefix = "bvhi_at_";
    private const string RefreshTokenPrefix = "bvhi_rt_";
    private const string AuthorizationCodePrefix = "bvhi_ac_";

    private readonly AppDbContext _db;
    private readonly FileStorageService _storage;
    private readonly HouseholdIntegrationSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<string> _redirectUris;
    private readonly HashSet<string> _allowedScopes;

    public HouseholdIntegrationService(
        AppDbContext db,
        FileStorageService storage,
        IOptions<HouseholdIntegrationSettings> settings,
        TimeProvider timeProvider)
    {
        _db = db;
        _storage = storage;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _redirectUris = new HashSet<string>(_settings.RedirectUris, StringComparer.Ordinal);
        _allowedScopes = new HashSet<string>(HouseholdIntegrationSettings.AllowedScopes, StringComparer.Ordinal);
    }

    public async Task<HouseholdServiceResult<HouseholdAuthorizeResponse>> AuthorizeAsync(
        int userId,
        HouseholdAuthorizeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsRegisteredClient(request.ClientId) || !IsRegisteredRedirect(request.RedirectUri))
        {
            return HouseholdServiceResult<HouseholdAuthorizeResponse>.Fail(
                "invalid_request",
                "The client or redirect URI is not registered.");
        }

        if (!IsValidState(request.State))
        {
            return RedirectAuthorizationError(request.RedirectUri, request.State, "invalid_request");
        }

        if (!request.Approved)
        {
            return HouseholdServiceResult<HouseholdAuthorizeResponse>.Success(
                new HouseholdAuthorizeResponse(BuildRedirect(request.RedirectUri, request.State, error: "access_denied")));
        }

        if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal) ||
            !IsValidPkceValue(request.CodeChallenge, 43, 43))
        {
            return RedirectAuthorizationError(request.RedirectUri, request.State, "invalid_request");
        }

        var scopes = NormalizeScopes(request.Scopes);
        if (scopes is null)
        {
            return RedirectAuthorizationError(request.RedirectUri, request.State, "invalid_scope");
        }

        if (!await _db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return HouseholdServiceResult<HouseholdAuthorizeResponse>.Fail(
                "access_denied",
                "The source account is not available.");
        }

        var now = UtcNow;
        var rawCode = CreateRandomToken(AuthorizationCodePrefix);
        var connection = new HouseholdConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = _settings.ClientId,
            GrantedScopes = string.Join(' ', scopes),
            Status = HouseholdConnectionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        connection.AuthorizationCodes.Add(new HouseholdAuthorizationCode
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            CodeHash = HashCredential(rawCode),
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            ExpiresAt = now.AddMinutes(_settings.AuthorizationCodeMinutes)
        });

        _db.HouseholdConnections.Add(connection);
        await _db.SaveChangesAsync(cancellationToken);

        return HouseholdServiceResult<HouseholdAuthorizeResponse>.Success(
            new HouseholdAuthorizeResponse(BuildRedirect(request.RedirectUri, request.State, rawCode)));
    }

    public Task<HouseholdServiceResult<HouseholdTokenResponse>> TokenAsync(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsRegisteredClient(request.ClientId))
        {
            return Task.FromResult(HouseholdServiceResult<HouseholdTokenResponse>.Fail(
                "invalid_client",
                "The client is not registered."));
        }

        return request.GrantType switch
        {
            "authorization_code" => ExchangeAuthorizationCodeAsync(request, cancellationToken),
            "refresh_token" => RefreshAsync(request, cancellationToken),
            _ => Task.FromResult(HouseholdServiceResult<HouseholdTokenResponse>.Fail(
                "unsupported_grant_type",
                "The grant type is not supported."))
        };
    }

    public async Task RevokeAsync(
        HouseholdRevokeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 512)
        {
            return;
        }

        var tokenHash = HashCredential(request.Token);
        Guid? connectionId = null;

        if (!string.Equals(request.TokenTypeHint, "refresh_token", StringComparison.Ordinal))
        {
            connectionId = await _db.HouseholdAccessTokens
                .Where(token => token.TokenHash == tokenHash)
                .Select(token => (Guid?)token.ConnectionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        connectionId ??= await _db.HouseholdRefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.ConnectionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (connectionId is null && string.Equals(request.TokenTypeHint, "refresh_token", StringComparison.Ordinal))
        {
            connectionId = await _db.HouseholdAccessTokens
                .Where(token => token.TokenHash == tokenHash)
                .Select(token => (Guid?)token.ConnectionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (connectionId is null)
        {
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await RevokeConnectionAsync(connectionId.Value, UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HouseholdMeResponse?> GetMeAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _db.HouseholdConnections
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(
                item => item.Id == connectionId && item.Status == HouseholdConnectionStatus.Active,
                cancellationToken);

        if (connection is null)
        {
            return null;
        }

        return new HouseholdMeResponse(
            connection.Id,
            BuildAccount(connection.User),
            SplitScopes(connection.GrantedScopes));
    }

    public async Task<HouseholdPokemonDownload?> GetPokemonDownloadAsync(
        int userId,
        int pokemonId,
        CancellationToken cancellationToken = default)
    {
        var file = await _db.Pokemon
            .AsNoTracking()
            .Where(pokemon =>
                pokemon.Id == pokemonId &&
                pokemon.UserId == userId &&
                pokemon.File.UserId == userId)
            .Select(pokemon => new
            {
                pokemon.File.StoredPath,
                pokemon.File.OriginalFileName,
                pokemon.File.FileName,
                pokemon.File.Format,
                pokemon.File.RawBlob
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            return null;
        }

        byte[]? content = null;
        if (_storage.TryReadUserFile(userId, file.StoredPath, out var storedContent))
        {
            content = storedContent;
        }
        else if (file.RawBlob is { Length: > 0 })
        {
            content = file.RawBlob;
        }

        if (content is null) return null;

        var fileName = SanitizeDownloadFileName(
            file.OriginalFileName ?? file.FileName,
            file.Format,
            content,
            pokemonId,
            file.StoredPath);
        return new HouseholdPokemonDownload(content, fileName);
    }

    private async Task<HouseholdServiceResult<HouseholdTokenResponse>> ExchangeAuthorizationCodeAsync(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 512 ||
            string.IsNullOrWhiteSpace(request.CodeVerifier) ||
            !IsValidPkceValue(request.CodeVerifier, 43, 128) ||
            string.IsNullOrWhiteSpace(request.RedirectUri) ||
            !IsRegisteredRedirect(request.RedirectUri))
        {
            return InvalidGrant();
        }

        var codeHash = HashCredential(request.Code);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var code = await _db.HouseholdAuthorizationCodes
            .Include(item => item.Connection)
            .ThenInclude(connection => connection.User)
            .FirstOrDefaultAsync(item => item.CodeHash == codeHash, cancellationToken);

        var now = UtcNow;
        if (code is null ||
            code.ConsumedAt is not null ||
            code.ExpiresAt <= now ||
            code.Connection.Status != HouseholdConnectionStatus.Active ||
            !string.Equals(code.Connection.ClientId, request.ClientId, StringComparison.Ordinal) ||
            !string.Equals(code.RedirectUri, request.RedirectUri, StringComparison.Ordinal) ||
            !VerifyPkce(code.CodeChallenge, request.CodeVerifier))
        {
            return InvalidGrant();
        }

        var consumed = await _db.HouseholdAuthorizationCodes
            .Where(item => item.Id == code.Id && item.ConsumedAt == null && item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                update => update.SetProperty(item => item.ConsumedAt, now),
                cancellationToken);

        if (consumed != 1)
        {
            return InvalidGrant();
        }

        var familyId = Guid.NewGuid();
        var issued = IssueTokenPair(code.Connection, familyId, now);
        code.Connection.LastUsedAt = now;
        code.Connection.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return HouseholdServiceResult<HouseholdTokenResponse>.Success(
            BuildTokenResponse(code.Connection, issued, now));
    }

    private async Task<HouseholdServiceResult<HouseholdTokenResponse>> RefreshAsync(
        HouseholdTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.RefreshToken.Length > 512)
        {
            return InvalidGrant();
        }

        var tokenHash = HashCredential(request.RefreshToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var current = await _db.HouseholdRefreshTokens
            .Include(item => item.Connection)
            .ThenInclude(connection => connection.User)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        var now = UtcNow;
        if (current is null ||
            current.Connection.Status != HouseholdConnectionStatus.Active ||
            !string.Equals(current.Connection.ClientId, request.ClientId, StringComparison.Ordinal))
        {
            return InvalidGrant();
        }

        if (current.RevokedAt is not null || current.ReplacedByTokenId is not null)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InvalidGrant();
        }

        if (current.ExpiresAt <= now)
        {
            current.RevokedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InvalidGrant();
        }

        var replacementId = Guid.NewGuid();
        var issued = IssueTokenPair(current.Connection, current.FamilyId, now, replacementId);
        await _db.SaveChangesAsync(cancellationToken);

        var rotated = await _db.HouseholdRefreshTokens
            .Where(item => item.Id == current.Id && item.RevokedAt == null && item.ReplacedByTokenId == null)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(item => item.RevokedAt, now)
                    .SetProperty(item => item.ReplacedByTokenId, replacementId),
                cancellationToken);

        if (rotated != 1)
        {
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InvalidGrant();
        }

        current.Connection.LastUsedAt = now;
        current.Connection.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return HouseholdServiceResult<HouseholdTokenResponse>.Success(
            BuildTokenResponse(current.Connection, issued, now));
    }

    private IssuedTokenPair IssueTokenPair(
        HouseholdConnection connection,
        Guid familyId,
        DateTime now,
        Guid? refreshTokenId = null)
    {
        var accessToken = CreateRandomToken(AccessTokenPrefix);
        var refreshToken = CreateRandomToken(RefreshTokenPrefix);
        var accessExpiresAt = now.AddMinutes(_settings.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(_settings.RefreshTokenDays);

        _db.HouseholdAccessTokens.Add(new HouseholdAccessToken
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            FamilyId = familyId,
            TokenHash = HashCredential(accessToken),
            ExpiresAt = accessExpiresAt
        });
        _db.HouseholdRefreshTokens.Add(new HouseholdRefreshToken
        {
            Id = refreshTokenId ?? Guid.NewGuid(),
            ConnectionId = connection.Id,
            FamilyId = familyId,
            TokenHash = HashCredential(refreshToken),
            ExpiresAt = refreshExpiresAt
        });

        return new IssuedTokenPair(accessToken, refreshToken, accessExpiresAt, refreshExpiresAt);
    }

    private HouseholdTokenResponse BuildTokenResponse(
        HouseholdConnection connection,
        IssuedTokenPair issued,
        DateTime now) =>
        new(
            "Bearer",
            issued.AccessToken,
            Math.Max(0, (int)(issued.AccessExpiresAt - now).TotalSeconds),
            issued.RefreshToken,
            Math.Max(0, (int)(issued.RefreshExpiresAt - now).TotalSeconds),
            connection.GrantedScopes,
            connection.Id,
            BuildAccount(connection.User));

    private async Task RevokeConnectionAsync(
        Guid connectionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _db.HouseholdConnections
            .Where(connection => connection.Id == connectionId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(connection => connection.Status, HouseholdConnectionStatus.Revoked)
                .SetProperty(connection => connection.RevokedAt, now)
                .SetProperty(connection => connection.UpdatedAt, now), cancellationToken);
        await _db.HouseholdAccessTokens
            .Where(token => token.ConnectionId == connectionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await _db.HouseholdRefreshTokens
            .Where(token => token.ConnectionId == connectionId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await _db.HouseholdAuthorizationCodes
            .Where(code => code.ConnectionId == connectionId && code.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(code => code.ConsumedAt, now), cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        await _db.HouseholdRefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await _db.HouseholdAccessTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    private HouseholdServiceResult<HouseholdAuthorizeResponse> RedirectAuthorizationError(
        string redirectUri,
        string state,
        string error) =>
        HouseholdServiceResult<HouseholdAuthorizeResponse>.Success(
            new HouseholdAuthorizeResponse(BuildRedirect(redirectUri, state, error: error)));

    private bool IsRegisteredClient(string clientId) =>
        !string.IsNullOrWhiteSpace(_settings.ClientId) &&
        string.Equals(clientId, _settings.ClientId, StringComparison.Ordinal);

    private bool IsRegisteredRedirect(string redirectUri) =>
        !string.IsNullOrWhiteSpace(redirectUri) && _redirectUris.Contains(redirectUri);

    private static bool IsValidState(string state) =>
        !string.IsNullOrWhiteSpace(state) && state.Length <= 512;

    private string[]? NormalizeScopes(IReadOnlyList<string>? scopes)
    {
        if (scopes is null || scopes.Count == 0 || scopes.Any(scope => !_allowedScopes.Contains(scope)))
        {
            return null;
        }

        var requested = new HashSet<string>(scopes, StringComparer.Ordinal);
        return HouseholdIntegrationSettings.AllowedScopes.Where(requested.Contains).ToArray();
    }

    private static bool IsValidPkceValue(string value, int minimumLength, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minimumLength &&
        value.Length <= maximumLength &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~');

    private static bool VerifyPkce(string expectedChallenge, string verifier)
    {
        var actualChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedChallenge),
            Encoding.ASCII.GetBytes(actualChallenge));
    }

    public static string HashCredential(string credential) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));

    private static string CreateRandomToken(string prefix) =>
        prefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string BuildRedirect(
        string redirectUri,
        string state,
        string? code = null,
        string? error = null)
    {
        var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var parameter = code is not null
            ? $"code={Uri.EscapeDataString(code)}"
            : $"error={Uri.EscapeDataString(error ?? "invalid_request")}";
        return $"{redirectUri}{separator}{parameter}&state={Uri.EscapeDataString(state)}";
    }

    private static HouseholdAccountDto BuildAccount(User user)
    {
        var accountHash = SHA256.HashData(
            Encoding.UTF8.GetBytes($"beast-vault:household-account:{user.Id}"));
        var opaqueId = Base64UrlEncode(accountHash[..16]);
        return new HouseholdAccountDto($"bvacct_{opaqueId}", user.Username);
    }

    private static string[] SplitScopes(string scopes) =>
        scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string SanitizeDownloadFileName(string? fileName, string format, byte[] content, int pokemonId, string? fallbackFileName = null)
    {
        var normalized = (fileName ?? string.Empty).Replace('\\', '/');
        var leafName = normalized[(normalized.LastIndexOf('/') + 1)..];
        var safeName = new string(leafName
            .Select(character =>
                char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                    ? '_'
                    : character)
            .ToArray())
            .Trim()
            .Trim('.');

        if (safeName.Length > 180)
        {
            safeName = safeName[..180].TrimEnd(' ', '.');
        }

        if (!string.IsNullOrWhiteSpace(safeName))
        {
            var extension = Path.GetExtension(safeName);
            if (string.Equals(extension, ".pkm", StringComparison.OrdinalIgnoreCase))
            {
                var detectedFormat = DetectPokemonFormat(content) ?? GetPokemonExtension(fallbackFileName);
                if (detectedFormat is not null)
                    return Path.ChangeExtension(safeName, detectedFormat);
            }
            return safeName;
        }

        var safeFormat = DetectPokemonFormat(content) ?? GetPokemonExtension(fallbackFileName) ?? new string(format
            .Where(char.IsAsciiLetterOrDigit)
            .Take(10)
            .ToArray())
            .ToLowerInvariant();
        return $"pokemon-{pokemonId}.{(safeFormat.Length == 0 ? "pkm" : safeFormat)}";
    }

    private static string? DetectPokemonFormat(byte[] content)
    {
        try
        {
            var typeName = PKHeX.Core.EntityFormat.GetFromBytes(content)?.GetType().Name;
            return typeName is not null && typeName.Length is >= 3 and <= 4
                && typeName.StartsWith('P')
                && typeName.All(char.IsAsciiLetterOrDigit)
                ? typeName.ToLowerInvariant()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetPokemonExtension(string? fileName)
    {
        var extension = (Path.GetExtension(fileName) ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return extension is "pk1" or "pk2" or "pk3" or "pk4" or "pk5" or "pk6" or "pk7" or "pk8" or "pk9"
            or "pb7" or "pb8" or "pb9" or "pa8" or "pa9"
            ? extension
            : null;
    }

    private static HouseholdServiceResult<HouseholdTokenResponse> InvalidGrant() =>
        HouseholdServiceResult<HouseholdTokenResponse>.Fail(
            "invalid_grant",
            "The authorization grant is invalid or expired.");

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private sealed record IssuedTokenPair(
        string AccessToken,
        string RefreshToken,
        DateTime AccessExpiresAt,
        DateTime RefreshExpiresAt);
}
