namespace MoneyManager.Api.Models
{
    /// <summary>
    /// The currencies this product accepts. Mirrors <c>money-manager-ui/src/utils/currencies.ts</c>
    /// — deliberately a short list rather than every ISO 4217 code, because a landlord picks from
    /// the handful their portfolio actually uses, and an unchecked free-text code is how
    /// "NOT-A-CURRENCY" ends up stored as a currency.
    ///
    /// <para>
    /// This is input policy, so it lives here rather than inside the converter: the converter
    /// stays a pure function over whatever rates it is handed.
    /// </para>
    /// </summary>
    public static class SupportedCurrencies
    {
        public static readonly IReadOnlyCollection<string> All =
        [
            "EUR", "HUF", "USD", "GBP", "CHF", "PLN", "CZK", "RON",
        ];

        private static readonly HashSet<string> Lookup = new(All, StringComparer.Ordinal);

        /// <summary>The canonical upper-case form of a supported code, or null if it is not one.</summary>
        public static string? Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var candidate = code.Trim().ToUpperInvariant();
            return Lookup.Contains(candidate) ? candidate : null;
        }

        public static bool IsSupported(string? code) => Normalize(code) is not null;
    }
}
