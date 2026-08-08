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
    public sealed class CurrencyRollupService(MoneyManagerDbContext context, ICurrentUser currentUser)
    {
        /// <summary>Used when there is no user row to read a preference from. Never reached under the fallback authorization policy.</summary>
        private const string FallbackBaseCurrency = "EUR";

        public async Task<RollupContext> LoadAsync()
        {
            // Materialised before any arithmetic: SQLite has no decimal type, so aggregating a
            // money column in SQL loses precision. The rates are filtered to the requesting user
            // by the global query filter, like every other owned entity.
            var rates = await context.ExchangeRates.ToListAsync();

            var converter = new CurrencyConverter(rates.Select(r =>
                new ExchangeRateSnapshot(r.BaseCurrency, r.QuoteCurrency, r.Rate, r.AsOf)));

            var userId = currentUser.UserId;
            var user = userId is null
                ? null
                : await context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);

            return new RollupContext(
                converter,
                SupportedCurrencies.Normalize(user?.BaseCurrency) ?? FallbackBaseCurrency,
                user?.AlwaysConvertToBaseCurrency ?? false);
        }
    }
}
