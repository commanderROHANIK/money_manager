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

            // `force` skips the window above but not this one. Without a floor, the refresh button
            // is an authenticated user's lever for making this deployment call somebody else's API
            // as fast as they can click — and CLAUDE.md's precondition for having an outbound call
            // at all is that a cache limits it, which `force` would otherwise be an exception to.
            //
            // A minute rather than a rate limiter because UseRateLimiter runs ahead of
            // UseAuthentication, so a policy partitioned by user would see no principal and hand
            // the whole deployment one shared bucket. The endpoint still answers with the table
            // either way, and the ECB publishes once a working day, so a second press inside the
            // floor could not have produced a different number.
            var forcedKey = $"fx:forced:{userId}";
            if (force && cache.TryGetValue(forcedKey, out _))
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
                Remember(cacheKey, forcedKey, force);
                return 0;
            }

            var fetched = await provider.GetRatesAsync(target, wanted, cancellationToken);

            // Cached even when the provider gave nothing, so an outage does not turn every page
            // load into another timeout. The rates already stored keep working in the meantime.
            Remember(cacheKey, forcedKey, force);

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
                //
                // Matched in either direction, because a pair is one fact and that is already how
                // Upsert, Delete and the converter all treat it. Matching only the direction just
                // fetched looks harmless and is not: the unique index is on
                // (UserId, Base, Quote), so EUR→HUF and HUF→EUR are two different keys and
                // nothing stops both existing. Change your reporting currency from EUR to HUF and
                // the next refresh inserts the mirror image of a row you already had — after
                // which the table shows one pair twice, the converter prefers whichever direction
                // is asked for while the other drifts, and a Manual row is shadowed by a fetched
                // one that reads the other way round. That last part would break the rule this
                // whole service exists to keep.
                var row = existing.FirstOrDefault(r =>
                    (Matches(r.BaseCurrency, from) && Matches(r.QuoteCurrency, to))
                    || (Matches(r.BaseCurrency, to) && Matches(r.QuoteCurrency, from)));

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

                // Now correctly covers a manual row stored the other way round, which the
                // one-directional lookup above silently did not.
                if (row.Source == ExchangeRateSource.Manual)
                    continue;

                // Rewritten into the direction just fetched rather than inverted into the stored
                // one. Both express the same fact; storing the figure the provider actually gave
                // avoids a reciprocal, and a reciprocal is a second place for the number to lose
                // precision on its way to a total.
                row.BaseCurrency = from;
                row.QuoteCurrency = to;
                row.Rate = rate.Rate;
                row.AsOf = rate.AsOf;
                row.Source = rate.Source;
                written += 1;
            }

            if (written == 0)
                return 0;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Two requests refreshing the same rate-less account at once both read an empty
                // table and both insert; the unique index rejects the loser. `force` skips the
                // window that would otherwise have made this vanishingly unlikely, so a
                // double-clicked refresh button is enough to reach it.
                //
                // Swallowed rather than surfaced because the caller's answer is unchanged: the
                // rate is on record, written by the request that got there first. Failing the
                // dashboard over having lost a race to store a number that is now stored anyway
                // would be the opposite of the "failure is ordinary" stance the provider takes.
                logger.LogWarning(ex, "Could not store refreshed rates against {Base}.", target);
                return 0;
            }

            logger.LogInformation("Refreshed {Count} exchange rate(s) against {Base}.", written, target);

            return written;
        }

        /// <summary>
        /// Marks the ordinary window, and the short floor on explicit refreshes when that is what
        /// this was.
        /// </summary>
        private void Remember(string cacheKey, string forcedKey, bool force)
        {
            cache.Set(cacheKey, true, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));

            if (force)
                cache.Set(forcedKey, true, TimeSpan.FromMinutes(Math.Max(1, _options.ForcedRefreshMinutes)));
        }

        private static bool Matches(string stored, string code) =>
            string.Equals(stored, code, StringComparison.OrdinalIgnoreCase);

        private static string Pair(string from, string to) =>
            $"{from.Trim().ToUpperInvariant()}>{to.Trim().ToUpperInvariant()}";
    }
}
