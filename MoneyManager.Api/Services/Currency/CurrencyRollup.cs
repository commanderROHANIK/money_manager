using MoneyManager.Api.Services.Analytics;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// The result of totalling one metric across a portfolio.
    ///
    /// <para>
    /// <c>Blocked</c> is the distinction that keeps this honest. A null <c>Amount</c> with
    /// <c>Blocked = false</c> means nothing contributed a figure — the same "cannot be known"
    /// this codebase has always reported. A null <c>Amount</c> with <c>Blocked = true</c> means
    /// a figure existed but no rate could express it in the target currency. Both render as
    /// unknown; only the second one is worth telling the user how to fix.
    /// </para>
    /// </summary>
    public readonly record struct RollupTotal(decimal? Amount, bool Blocked);

    /// <summary>
    /// Totalling across currencies, in one place, so the portfolio rollup and the bank-balance
    /// rollup cannot drift apart. Pure — it is handed the rates and the target currency.
    /// </summary>
    public static class CurrencyRollup
    {
        public const string MissingRateWarningCode = "missing_exchange_rate";

        /// <summary>
        /// Which currency a set of figures should be reported in.
        ///
        /// <para>
        /// A set that already shares one currency is reported in that currency and needs no rate
        /// at all, unless the user has asked for everything in their base currency. Converting a
        /// homogeneous portfolio by default would strand a HUF-only landlord whose base currency
        /// happens to say EUR, for no gain.
        /// </para>
        /// </summary>
        public static string ResolveTarget(
            IReadOnlyCollection<string> currencies,
            string baseCurrency,
            bool alwaysConvertToBaseCurrency)
        {
            if (currencies.Count == 1 && !alwaysConvertToBaseCurrency)
                return currencies.First();

            return baseCurrency;
        }

        /// <summary>
        /// Totals <paramref name="contributions"/> in <paramref name="target"/>.
        ///
        /// <para>
        /// A contribution that is null is skipped, exactly as before conversion existed: the
        /// property simply has nothing to say about this metric. A contribution that has a value
        /// but no rate blocks the whole total — half a portfolio added up is not a portfolio
        /// total, and presenting one would be the confident wrong number in its purest form.
        /// </para>
        /// <para>
        /// Each contribution converts at full precision and only the total is rounded; rounding
        /// every leg first and adding the results drifts by a cent per property.
        /// </para>
        /// </summary>
        public static RollupTotal Sum(
            IEnumerable<(decimal? Amount, string CurrencyCode)> contributions,
            ICurrencyConverter rates,
            string target)
        {
            var sum = 0m;
            var contributed = false;

            foreach (var (amount, currencyCode) in contributions)
            {
                if (amount is not { } value)
                    continue;

                if (rates.Convert(value, new CurrencyPair(currencyCode, target)) is not { } converted)
                    return new RollupTotal(null, true);

                sum += converted;
                contributed = true;
            }

            return new RollupTotal(contributed ? Math.Round(sum, 2) : null, false);
        }

        /// <summary>Pairs that have no rate on record, so the user can be told exactly what to enter.</summary>
        public static IReadOnlyList<CurrencyPair> MissingRates(
            IEnumerable<string> sourceCurrencies,
            ICurrencyConverter rates,
            string target) =>
            DistinctPairs(sourceCurrencies, target)
                .Where(pair => rates.RateFor(pair) is null)
                .ToList();

        /// <summary>The rates a conversion leant on, so the UI can show its working.</summary>
        public static IReadOnlyList<AppliedRate> AppliedRates(
            IEnumerable<string> sourceCurrencies,
            ICurrencyConverter rates,
            string target) =>
            DistinctPairs(sourceCurrencies, target)
                .Select(rates.RateFor)
                .OfType<AppliedRate>()
                .ToList();

        /// <summary>
        /// The one message both rollups use, so "you are missing a rate" reads the same wherever
        /// it surfaces. Reuses <see cref="MetricWarning"/> rather than introducing a second
        /// (code, message) pair — it is already the shape the UI renders warnings from.
        /// </summary>
        public static MetricWarning MissingRateWarning(IReadOnlyList<CurrencyPair> missing)
        {
            var pairs = string.Join(", ", missing.Select(p => $"{p.From}→{p.To}"));

            return new MetricWarning(
                MissingRateWarningCode,
                $"No exchange rate on record for {pairs}, so totals spanning those currencies " +
                "cannot be worked out. Add the rate in Settings and they will appear.");
        }

        private static IEnumerable<CurrencyPair> DistinctPairs(IEnumerable<string> sourceCurrencies, string target) =>
            sourceCurrencies
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => c.Length > 0 && c != target)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal)
                .Select(c => new CurrencyPair(c, target));
    }
}
