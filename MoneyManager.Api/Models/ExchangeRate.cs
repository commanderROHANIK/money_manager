namespace MoneyManager.Api.Models
{
    /// <summary>
    /// One currency pair on one date.
    ///
    /// Deliberately NOT <see cref="IOwnedByUser"/>: rates are reference data, not tenant
    /// data. Giving them a tenant query filter would mean a background refresh, which runs
    /// with no current user, writes rows that nothing can subsequently read.
    ///
    /// Only one direction of a pair is stored — the inverse is derived — so the two can
    /// never drift out of agreement with each other.
    /// </summary>
    public class ExchangeRate
    {
        public int Id { get; set; }

        /// <summary>ISO 4217, upper case.</summary>
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;

        /// <summary>Units of <see cref="ToCurrency"/> per one unit of <see cref="FromCurrency"/>.</summary>
        public decimal Rate { get; set; }

        public DateOnly AsOf { get; set; }

        /// <summary>"manual" when a user entered it, otherwise the provider key.</summary>
        public string Source { get; set; } = "manual";
    }
}
