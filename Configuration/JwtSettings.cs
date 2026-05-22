namespace BeastVault.Api.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "BeastVault.Api";
    public string Audience { get; set; } = "BeastVault.Client";
    public int AccessTokenMinutes { get; set; } = 10080; // 7 days
    public int RefreshTokenDays { get; set; } = 30;
}
