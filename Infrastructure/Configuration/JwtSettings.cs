namespace BeastVault.Api.Infrastructure.Configuration
{
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "BeastVault.Api";
        public string Audience { get; set; } = "BeastVault.Client";
        public int ExpirationMinutes { get; set; } = 525600; // 1 year
    }
}
