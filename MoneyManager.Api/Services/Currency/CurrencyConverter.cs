namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// Pure, like <see cref="Analytics.PropertyAnalyticsCalculator"/>: no DbContext, no clock, no
    /// configuration. Every rate it will ever see arrives in the constructor, which is what makes
    /// each rule below checkable against a literal in <c>CurrencyConverterTests</c>.
    /// </summary>
    public sealed class CurrencyConverter : ICurrencyConverter
    {
        private readonly Dictionary<CurrencyPair, ExchangeRateSnapshot> _rates = [];

        public CurrencyConverter(IEnumerable<ExchangeRateSnapshot> rates)
        {
            foreach (var rate in rates)
            {
                // A non-positive rate cannot be inverted and cannot express an exchange. Treating
                // such a row as absent yields "cannot be known", which is the honest answer;
                // letting it through would yield a divide-by-zero or a sign flip instead.
                if (rate.Rate <= 0)
                    continue;

                var from = Normalize(rate.BaseCurrency);
                var to = Normalize(rate.QuoteCurrency);
                if (from is null || to is null || from == to)
                    continue;

                _rates[new CurrencyPair(from, to)] = rate;
            }
        }

        /// <summary>A converter that knows no rates, so every cross-currency answer is "unknown".</summary>
        public static ICurrencyConverter Empty { get; } = new CurrencyConverter([]);

        public AppliedRate? RateFor(CurrencyPair pair)
        {
            var from = Normalize(pair.From);
            var to = Normalize(pair.To);

            if (from is null || to is null)
                return null;

            if (from == to)
                return new AppliedRate(from, to, 1m, null, false);

            if (_rates.TryGetValue(new CurrencyPair(from, to), out var direct))
                return new AppliedRate(from, to, direct.Rate, direct.AsOf, false, direct.Source);

            // A HUF→EUR row already answers "what is a HUF worth in EUR"; the EUR→HUF question is
            // the same row read backwards. Making the user type both directions would be busywork
            // whose main product is two rows that disagree.
            if (_rates.TryGetValue(new CurrencyPair(to, from), out var inverse))
                return new AppliedRate(from, to, 1m / inverse.Rate, inverse.AsOf, true, inverse.Source);

            // No transitive chaining (EUR→USD via HUF). A rate the user never entered, carrying
            // the compounded error of two others, is precisely the confident wrong number this
            // product exists to avoid. An unknown pair is reported as unknown.
            return null;
        }

        public decimal? Convert(decimal amount, CurrencyPair pair)
        {
            var rate = RateFor(pair);
            return rate is null ? null : amount * rate.Rate;
        }

        // Case only. Which codes are acceptable is input policy and belongs to
        // SupportedCurrencies at the controller edge, not to the arithmetic.
        private static string? Normalize(string? code) =>
            string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }
}
