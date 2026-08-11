using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// Everything a rollup needs to know about the requesting user: the rates they have entered
    /// and how they have asked for consolidated totals to be reported.
    /// </summary>
    public sealed record RollupContext(
        ICurrencyConverter Rates,
        string BaseCurrency,
        bool AlwaysConvertToBaseCurrency)
    {
        public string ResolveTarget(IReadOnlyCollection<string> currencies) =>
            CurrencyRollup.ResolveTarget(currencies, BaseCurrency, AlwaysConvertToBaseCurrency);
    }

    /// <summary>
    /// Loads that context. One service rather than two injections, because a controller that
    /// fetched the rates but forgot the user's preference would silently report in the wrong
    /// currency.
    /// </summary>
    public sealed class CurrencyRollupService(
        MoneyManagerDbContext context,
        ICurrentUser currentUser,
        ExchangeRateRefreshService refresh)
    {
        /// <summary>Used when there is no user row to read a preference from. Never reached under the fallback authorization policy.</summary>
        private const string FallbackBaseCurrency = "EUR";

        public async Task<RollupContext> LoadAsync()
        {
            var userId = currentUser.UserId;
            var user = userId is null
                ? null
                : await context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);

            var baseCurrency = SupportedCurrencies.Normalize(user?.BaseCurrency) ?? FallbackBaseCurrency;

            // Refreshed here, before the rates are read, rather than only when the Settings screen
            // lists them.
            //
            // This is the choke point every rollup goes through, and it is the only place that
            // makes the feature true for the person it is for. Wired solely into
            // ExchangeRatesController, fetching only ever happened for someone who had already
            // opened Settings — so a landlord with a HUF flat and a EUR mortgage, who never went
            // looking for a rate table, kept seeing null totals and "Add the rate in Settings"
            // with automatic rates switched on and working.
            //
            // The cache is what makes this affordable: one outbound call per user per window, not
            // one per page load. The first dashboard load in each window waits for it, which is
            // why the provider's timeout is short and why an unreachable provider returns empty
            // rather than throwing — a rate service having a bad morning must cost a few seconds,
            // not the dashboard.
            await refresh.RefreshAsync(baseCurrency, SupportedCurrencies.All);

            // Materialised before any arithmetic: SQLite has no decimal type, so aggregating a
            // money column in SQL loses precision. The rates are filtered to the requesting user
            // by the global query filter, like every other owned entity.
            var rates = await context.ExchangeRates.ToListAsync();

            var converter = new CurrencyConverter(rates.Select(r =>
                new ExchangeRateSnapshot(r.BaseCurrency, r.QuoteCurrency, r.Rate, r.AsOf, r.Source)));

            return new RollupContext(
                converter,
                baseCurrency,
                user?.AlwaysConvertToBaseCurrency ?? false);
        }
    }
}
