namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Bound from the "Seed" configuration section.
    ///
    /// <para>
    /// Seeding exists because a preview environment starts with an empty volume, and an empty
    /// app cannot be reviewed — which is the entire point of having preview environments. With
    /// registration disabled it is also the only way an account comes to exist at all.
    /// </para>
    /// </summary>
    public class SeedOptions
    {
        public const string SectionName = "Seed";

        /// <summary>Matches what registration enforces, so the seeded account is not a weaker door.</summary>
        public const int MinimumPasswordLength = 8;

        public bool Enabled { get; set; }

        public string Username { get; set; } = "demo";

        public string Email { get; set; } = "demo@example.invalid";

        /// <summary>
        /// Deliberately has no default, and startup fails rather than inventing one.
        ///
        /// <para>
        /// With registration disabled this account is the whole way in, and preview URLs are
        /// public with no platform gate in front of them. A built-in default would ship one
        /// known credential to every environment that ever runs this image.
        /// </para>
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Whether to seed the demo portfolio alongside the account. Off makes sense for a
        /// long-lived environment holding real records; on is what makes a preview worth opening.
        /// </summary>
        public bool IncludeDemoData { get; set; } = true;
    }
}
