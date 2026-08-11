using System.Globalization;
using System.Text.Json.Serialization;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// The European Central Bank's daily reference rates, read through Frankfurter, which
    /// republishes them as JSON with no API key and no quota.
    ///
    /// <para>
    /// The ECB rather than a market data vendor because of what the number has to survive: a
    /// landlord asking why their portfolio total moved. "The ECB reference rate for 11 August" is
    /// an answer; "our FX provider said so" is not. It is also the rate Hungarian and euro-area
    /// accounting conventions already reach for.
    /// </para>
    ///
    /// <para>
    /// These are reference rates, published once each working day at around 16:00 CET, and no bank
    /// will give you exactly this. The UI says so rather than implying the figure is tradeable —
    /// which is the same rule the analytics warnings follow: say which inputs are soft.
    /// </para>
    ///
    /// <para>
    /// This is the only outbound call the application makes, it is made by the API rather than the
    /// browser, and it happens only when <c>Features:AutomaticExchangeRates</c> is on. The page
    /// still makes no third-party request, which is what the CSP and the self-hosted fonts are
    /// there to guarantee.
    /// </para>
    /// </summary>
    public sealed class EcbExchangeRateProvider : IExchangeRateProvider
    {
        private readonly HttpClient _http;
        private readonly ILogger<EcbExchangeRateProvider> _logger;

        public EcbExchangeRateProvider(HttpClient http, ILogger<EcbExchangeRateProvider> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ProvidedRate>> GetRatesAsync(
            string baseCurrency,
            IReadOnlyCollection<string> quoteCurrencies,
            CancellationToken cancellationToken = default)
        {
            var wanted = quoteCurrencies
                .Select(c => c?.Trim().ToUpperInvariant())
                .Where(c => !string.IsNullOrEmpty(c) && c != baseCurrency.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (wanted.Length == 0)
                return [];

            var url = $"latest?base={Uri.EscapeDataString(baseCurrency.Trim().ToUpperInvariant())}"
                      + $"&symbols={Uri.EscapeDataString(string.Join(',', wanted))}";

            try
            {
                var payload = await _http.GetFromJsonAsync<FrankfurterResponse>(url, cancellationToken);

                if (payload?.Rates is null || payload.Rates.Count == 0)
                {
                    // A 200 with nothing usable in it is not an error, but it is not a rate either.
                    _logger.LogWarning("Exchange rate provider returned no rates for {Base}.", baseCurrency);
                    return [];
                }

                var asOf = ParseDate(payload.Date);

                return payload.Rates
                    .Where(entry => entry.Value > 0)
                    .Select(entry => new ProvidedRate(
                        payload.Base ?? baseCurrency,
                        entry.Key,
                        entry.Value,
                        asOf,
                        ExchangeRateSource.Ecb))
                    .ToArray();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                          or System.Text.Json.JsonException)
            {
                // Unreachable, slow, or answering with something unexpected. All three mean the
                // same thing to the caller — carry on with the rates already stored — and none of
                // them is worth failing a dashboard over. Logged rather than swallowed silently,
                // because a provider that has been down for a week should be discoverable.
                _logger.LogWarning(ex, "Could not fetch exchange rates for {Base}.", baseCurrency);
                return [];
            }
        }

        /// <summary>
        /// The publication date the provider reported, as a date rather than an instant.
        ///
        /// <para>
        /// Parsed with <c>DateTimeStyles.None</c> and treated as unspecified on purpose: this is
        /// the day the ECB published, not a moment, and letting it become a UTC instant is how a
        /// rate acquires a timezone it never had and starts displaying as the previous day.
        /// </para>
        /// </summary>
        private static DateTime ParseDate(string? value) =>
            DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : DateTime.UtcNow.Date;

        private sealed record FrankfurterResponse
        {
            [JsonPropertyName("base")]
            public string? Base { get; init; }

            [JsonPropertyName("date")]
            public string? Date { get; init; }

            [JsonPropertyName("rates")]
            public Dictionary<string, decimal>? Rates { get; init; }
        }
    }

    /// <summary>Bound from the "ExchangeRateProvider" configuration section.</summary>
    public sealed class ExchangeRateProviderOptions
    {
        public const string SectionName = "ExchangeRateProvider";

        /// <summary>
        /// Configurable so a deployment can point at a mirror, and so a test can point at nothing.
        /// Trailing slash matters: <c>HttpClient</c> resolves the relative path against it.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.frankfurter.dev/v1/";

        /// <summary>
        /// Short on purpose. A dashboard waiting on a rate provider is a dashboard that looks
        /// broken, and the correct answer when this expires is the rates already stored.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// How long a fetched set is reused before asking again. The ECB publishes once a working
        /// day, so anything shorter than a few hours is spending requests to learn nothing.
        /// </summary>
        public int CacheHours { get; set; } = 6;
    }
}
