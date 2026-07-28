namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Bound from the "JwtSettings" configuration section. This is the single source of
    /// truth for signing and validation — previously the issuer and audience were hardcoded
    /// in the token provider while different values sat unused in appsettings.json, so
    /// turning validation on would have rejected every token the app issued.
    /// </summary>
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        /// <summary>Minimum key length for HS256. Shorter keys are rejected at startup.</summary>
        public const int MinimumSecretKeyBytes = 32;

        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public int ExpiryHours { get; set; } = 12;
    }
}
