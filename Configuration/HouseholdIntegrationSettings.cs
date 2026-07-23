namespace BeastVault.Api.Configuration;

public sealed class HouseholdIntegrationSettings
{
    public const string SectionName = "HouseholdIntegration";

    public string ClientId { get; set; } = "household";
    public string[] RedirectUris { get; set; } = [];
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int AuthorizationCodeMinutes { get; set; } = 5;

    public static readonly string[] AllowedScopes =
    [
        "profile.read",
        "pokemon.read",
        "pokemon.favorite.write",
        "pokemon.notes.write"
    ];
}
