using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// Brings the requesting user's fetched rates up to date, and leaves the ones they typed in
    /// alone.
    ///
    /// <para>
    /// That asymmetry is the whole design. A manual row is something the user asserted; a fetched
    /// row is something we looked up. Overwriting the first with the second would quietly discard
    /// a decision — a landlord who entered the rate their bank actually gave them on the day of a
    /// transfer means it, and a daily reference rate is not a correction to that.
    /// </para>
    ///
    /// <para>
    /// So "automatic" is not a mode. It is what happens for the pairs you have not expressed an
    /// opinion about, which also means it needs no per-user column and therefore no migration —
    /// the presence of a row and its <c>Source</c> carry everything.
    /// </para>
    /// </summary>
    public sealed class ExchangeRateRefreshService(
        MoneyManagerDbContext context,
        IExchangeRateProvider provider,
        IMemoryCache cache,
        ICurrentUser currentUser,
        IOptions<ExchangeRateProviderOptions> options,
        ILogger<ExchangeRateRefreshService> logger)
    {
        private readonly ExchangeRateProviderOptions _options = options.Value;

        /// <summary>
        /// Fetches and stores rates from <paramref name="baseCurrency"/> to every other currency
        /// the user holds, skipping any pair they have entered themselves.
        ///
        /// <para>
        /// Returns the number of rows written, which is zero on every path that could not or need
        /// not do anything: no user, nothing to convert, a cached result still warm, or a provider
        /// that answered with nothing. None of those is an error — the caller carries on with the
        /// rates already stored.
        /// </para>
        /// </summary>
        public async Task<int> RefreshAsync(
            string baseCurrency,
            IReadOnlyCollection<string> heldCurrencies,
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            if (currentUser.UserId is not { } userId)
                return 0;

            var target = SupportedCurrencies.Normalize(baseCurrency);
            if (target is null)
                return 0;

            // One fetch per user per window. The ECB publishes once a working day, so asking more
            // often spends requests to learn nothing — and a dashboard that refreshes on every
            // load would turn one visitor into a steady stream of outbound calls.
            // `force` is the explicit refresh button: someone asking for today's number should
            // not have to wait out a window they cannot see.
            var cacheKey = $"fx:{userId}:{target}";
            if (!force && cache.TryGetValue(cacheKey, out _))
                return 0;

            var existing = await context.ExchangeRates.ToListAsync(cancellationToken);

            // A pair the user has spoken for is not asked about at all, rather than fetched and
            // then discarded — the cheapest request is the one not made.
            var manual = existing
                .Where(r => r.Source == ExchangeRateSource.Manual)
                .SelectMany(r => new[] { Pair(r.BaseCurrency, r.QuoteCurrency), Pair(r.QuoteCurrency, r.BaseCurrency) })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var wanted = heldCurrencies
                .Select(SupportedCurrencies.Normalize)
                .OfType<string>()
                .Where(c => c != target && !manual.Contains(Pair(target, c)))
                .Distinct()
                .ToArray();

            if (wanted.Length == 0)
            {
                // Nothing to ask for is still a reason not to ask again for a while.
                Remember(cacheKey);
                return 0;
            }

            var fetched = await provider.GetRatesAsync(target, wanted, cancellationToken);

            // Cached even when the provider gave nothing, so an outage does not turn every page
            // load into another timeout. The rates already stored keep working in the meantime.
            Remember(cacheKey);

            if (fetched.Count == 0)
                return 0;

            var written = 0;

            foreach (var rate in fetched)
            {
                var from = SupportedCurrencies.Normalize(rate.BaseCurrency);
                var to = SupportedCurrencies.Normalize(rate.QuoteCurrency);

                if (from is null || to is null || from == to || rate.Rate <= 0)
                    continue;

                // Loaded through the filtered set and mutated, never attached from outside it —
                // the tenant isolation rule in CLAUDE.md, which is why this is a lookup in the
                // materialised list rather than an Entry(...).State assignment.
                var row = existing.FirstOrDefault(r =>
                    string.Equals(r.BaseCurrency, from, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.QuoteCurrency, to, StringComparison.OrdinalIgnoreCase));

                if (row is null)
                {
                    context.ExchangeRates.Add(new ExchangeRate
                    {
                        BaseCurrency = from,
                        QuoteCurrency = to,
                        Rate = rate.Rate,
                        AsOf = rate.AsOf,
                        Source = rate.Source,
                    });
                    written += 1;
                    continue;
                }

                if (row.Source == ExchangeRateSource.Manual)
                    continue;

                row.Rate = rate.Rate;
                row.AsOf = rate.AsOf;
                row.Source = rate.Source;
                written += 1;
            }

            if (written > 0)
                await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Refreshed {Count} exchange rate(s) against {Base}.", written, target);

            return written;
        }

        private void Remember(string cacheKey) =>
            cache.Set(cacheKey, true, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));

        private static string Pair(string from, string to) =>
            $"{from.Trim().ToUpperInvariant()}>{to.Trim().ToUpperInvariant()}";
    }
}
