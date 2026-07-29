namespace MoneyManager.Api.Models
{
    public class User
    {
        public int Id { get; set; }  // Primary Key
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Lookup keys, stored separately so the unique indexes are case-insensitive
        /// without depending on the database's collation.
        /// </summary>
        public string NormalizedEmail { get; set; } = string.Empty;
        public string NormalizedUsername { get; set; } = string.Empty;

        /// <summary>PBKDF2 hash produced by <c>PasswordHasher&lt;User&gt;</c>. Never the password itself.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>ISO 4217 code that consolidated portfolio totals are reported in.</summary>
        public string BaseCurrency { get; set; } = "EUR";

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
