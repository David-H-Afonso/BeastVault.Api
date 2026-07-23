namespace BeastVault.Api.Domain.Entities;

public enum HouseholdConnectionStatus
{
    Active = 0,
    Revoked = 1
}

public sealed class HouseholdConnection
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string GrantedScopes { get; set; } = string.Empty;
    public HouseholdConnectionStatus Status { get; set; } = HouseholdConnectionStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<HouseholdAuthorizationCode> AuthorizationCodes { get; set; } = [];
    public ICollection<HouseholdAccessToken> AccessTokens { get; set; } = [];
    public ICollection<HouseholdRefreshToken> RefreshTokens { get; set; } = [];
}

public sealed class HouseholdAuthorizationCode
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public HouseholdConnection Connection { get; set; } = null!;
}

public sealed class HouseholdAccessToken
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public HouseholdConnection Connection { get; set; } = null!;
}

public sealed class HouseholdRefreshToken
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public HouseholdConnection Connection { get; set; } = null!;
    public HouseholdRefreshToken? ReplacedByToken { get; set; }
}
