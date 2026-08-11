namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Where a rate came from, and therefore what a converted total can honestly claim about
    /// itself.
    ///
    /// <para>
    /// Persisted as an int on a column that already exists, so adding a member here is not a
    /// schema change — which is the whole reason the enum was introduced with one value before
    /// there was a second one to add.
    /// </para>
    /// </summary>
    public enum ExchangeRateSource
    {
        /// <summary>Typed in by the user. Always wins over a fetched rate for the same pair.</summary>
        Manual = 0,

        /// <summary>
        /// The European Central Bank's daily reference rates, fetched by the API.
        ///
        /// <para>
        /// Reference rates, not tradeable ones: the ECB publishes them once each working day at
        /// around 16:00 CET, and no bank will give you exactly this. That is a distinction the UI
        /// has to make rather than hide, which is why the source is stored rather than inferred.
        /// </para>
        /// </summary>
        Ecb = 1,
    }

    /// <summary>
    /// A conversion factor between two currencies, typed in by the user who owns it.
    ///
    /// <para>
    /// <see cref="Rate"/> reads as "one <see cref="BaseCurrency"/> buys this many
    /// <see cref="QuoteCurrency"/>". A HUF/EUR row at 0.0025 therefore says 1 HUF = 0.0025 EUR.
    /// One row per pair per user: the endpoint upserts, so there is never a set of rates for
    /// the same pair to disagree with each other. <see cref="AsOf"/> records when the figure
    /// was true so the UI can say how old the number it converted with is.
    /// </para>
    /// <para>
    /// <see cref="BaseCurrency"/> names one side of a pair and has nothing to do with
    /// <see cref="User.BaseCurrency"/>, which is the single currency a user wants consolidated
    /// totals reported in.
    /// </para>
    /// </summary>
    public class ExchangeRate : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public string BaseCurrency { get; set; } = string.Empty;
        public string QuoteCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public DateTime AsOf { get; set; }
        public ExchangeRateSource Source { get; set; } = ExchangeRateSource.Manual;
    }
}
