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

        /// <summary>
        /// May write the shared reference data every tenant reads — currently exchange
        /// rates. A wrong rate silently misstates every other user's portfolio total, and a
        /// deleted one withholds it, so this is not something an ordinary account gets.
        ///
        /// The first account registered on an instance is the administrator, which keeps a
        /// fresh deployment usable without a separate provisioning step.
        /// </summary>
        public bool IsAdmin { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
