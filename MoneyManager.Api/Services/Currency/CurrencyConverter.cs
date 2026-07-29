using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// The result of a conversion. <see cref="RateAsOf"/> is null when no rate was needed
    /// (converting a currency to itself), so an identity conversion never drags down the
    /// "as at" date reported for a total.
    /// </summary>
    public readonly record struct Converted(decimal Amount, DateOnly? RateAsOf);

    /// <summary>
    /// Converts between currencies from a fixed snapshot of rates.
    ///
    /// Pure and immutable by design: build it once per request from whatever rates are
    /// stored, then every conversion in that request uses the same view. That makes the
    /// crossing rules testable with literals, and stops one figure in a total being
    /// converted at a different rate from the next.
    ///
    /// Returns null rather than falling back to 1:1 when a pair cannot be reached. Treating
    /// an unknown rate as parity is the single worst bug available here — it silently
    /// reports a Hungarian portfolio as if forints were euros.
    /// </summary>
    public sealed class CurrencyConverter
    {
        public const string DefaultPivot = "EUR";

        private readonly Dictionary<(string From, string To), (decimal Rate, DateOnly AsOf)> _rates;
        private readonly string _pivot;

        private CurrencyConverter(
            Dictionary<(string, string), (decimal, DateOnly)> rates, string pivot)
        {
            _rates = rates;
            _pivot = pivot;
        }

        /// <summary>
        /// Builds a converter, keeping only the most recent rate for each pair and ignoring
        /// non-positive rates, which cannot be inverted and are always bad data.
        /// </summary>
        public static CurrencyConverter FromRates(
            IEnumerable<ExchangeRate> rates, string pivot = DefaultPivot)
        {
            var latest = new Dictionary<(string, string), (decimal Rate, DateOnly AsOf)>();

            foreach (var rate in rates)
            {
                if (rate.Rate <= 0m)
                    continue;

                var key = (Normalise(rate.FromCurrency), Normalise(rate.ToCurrency));
                if (key.Item1 == key.Item2)
                    continue;

                if (!latest.TryGetValue(key, out var existing) || rate.AsOf > existing.AsOf)
                    latest[key] = (rate.Rate, rate.AsOf);
            }

            return new CurrencyConverter(latest, Normalise(pivot));
        }

        public static CurrencyConverter Empty(string pivot = DefaultPivot) =>
            new([], Normalise(pivot));

        public bool CanConvert(string from, string to) => Convert(1m, from, to) is not null;

        /// <summary>Null means the pair cannot be reached from the stored rates.</summary>
        public Converted? Convert(decimal amount, string from, string to)
        {
            var source = Normalise(from);
            var target = Normalise(to);

            if (source == target)
                return new Converted(amount, null);

            if (FindRate(source, target) is { } direct)
                return new Converted(amount * direct.Rate, direct.AsOf);

            // No direct or inverse pair: cross through the pivot, which is how a real rate
            // table works — everything is quoted against one currency, not against everything.
            if (source != _pivot && target != _pivot
                && FindRate(source, _pivot) is { } toPivot
                && FindRate(_pivot, target) is { } fromPivot)
            {
                // The total is only as current as the staler of the two legs.
                var asOf = toPivot.AsOf < fromPivot.AsOf ? toPivot.AsOf : fromPivot.AsOf;
                return new Converted(amount * toPivot.Rate * fromPivot.Rate, asOf);
            }

            return null;
        }

        private (decimal Rate, DateOnly AsOf)? FindRate(string from, string to)
        {
            if (_rates.TryGetValue((from, to), out var direct))
                return direct;

            if (_rates.TryGetValue((to, from), out var inverse))
                return (1m / inverse.Rate, inverse.AsOf);

            return null;
        }

        private static string Normalise(string code) =>
            (code ?? string.Empty).Trim().ToUpperInvariant();
    }
}
