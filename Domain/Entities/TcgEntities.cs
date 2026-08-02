namespace BeastVault.Api.Domain.Entities;

public class TcgSetEntity
{
    public int Id { get; set; }
    public string Provider { get; set; } = "tcgdex";
    public string ProviderSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Series { get; set; }
    public string? SeriesId { get; set; }
    public string? OfficialCode { get; set; }
    public int PrintedTotal { get; set; }
    public int Total { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? SymbolUrl { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CardsSyncedAt { get; set; }

    public ICollection<TcgCardEntity> Cards { get; set; } = [];
}

public class TcgCardEntity
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public string Provider { get; set; } = "tcgdex";
    public string ProviderCardId { get; set; } = string.Empty;
    public string? PokemonTcgIoId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Rarity { get; set; }
    public string? Artist { get; set; }
    public string? ImageSmall { get; set; }
    public string? ImageLarge { get; set; }
    public string NationalPokedexNumbersJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[\"normal\"]";
    public decimal? PriceEur { get; set; }
    public decimal? PriceUsd { get; set; }
    public string VariantPricesEurJson { get; set; } = "{}";
    public string VariantPricesUsdJson { get; set; } = "{}";
    public DateTime? PriceUpdatedAt { get; set; }
    public DateTime? PriceCheckedAt { get; set; }
    public string? LastRefreshError { get; set; }
    public string? CardmarketUrl { get; set; }
    public string? TcgplayerUrl { get; set; }
    public string ProviderMetadataJson { get; set; } = "{}";
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DetailedAt { get; set; }

    public TcgSetEntity Set { get; set; } = null!;
    public ICollection<UserTcgCardEntity> OwnedEntries { get; set; } = [];
}

public class UserTcgCardEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CardId { get; set; }
    public string Variant { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public TcgCardEntity Card { get; set; } = null!;
}

public class UserApiCredentialEntity
{
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProtectedValue { get; set; } = string.Empty;
    public string LastFour { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
