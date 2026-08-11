using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>A rate as a provider reported it, before anything is stored.</summary>
    public readonly record struct ProvidedRate(
        string BaseCurrency,
        string QuoteCurrency,
        decimal Rate,
        DateTime AsOf,
        ExchangeRateSource Source);

    /// <summary>
    /// Where automatically fetched rates come from.
    ///
    /// <para>
    /// A seam for the same reason <c>IBankDataProvider</c> is one: assuming today's source will be
    /// replaced is the safe assumption, and nothing above this interface should have to change
    /// when it is. It is also what lets a deployment that must not reach the internet register the
    /// no-op below and behave exactly as the app did before rates were ever fetched.
    /// </para>
    /// </summary>
    public interface IExchangeRateProvider
    {
        /// <summary>
        /// The current rate for each requested pair, or an empty list if none could be obtained.
        ///
        /// <para>
        /// Returning empty rather than throwing is deliberate. A rate provider being unreachable
        /// is an ordinary condition — the network is not a dependency this product can insist on —
        /// and the caller's correct response is to carry on with the rates it already has, not to
        /// fail a dashboard.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<ProvidedRate>> GetRatesAsync(
            string baseCurrency,
            IReadOnlyCollection<string> quoteCurrencies,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The default, and what an offline deployment gets: no rates, and no outbound call attempted.
    ///
    /// <para>
    /// Registered whenever <c>Features:AutomaticExchangeRates</c> is off, which keeps the promise
    /// that switching the feature off is not merely cosmetic — nothing is fetched, and there is no
    /// DNS lookup to observe either.
    /// </para>
    /// </summary>
    public sealed class NoExchangeRateProvider : IExchangeRateProvider
    {
        public Task<IReadOnlyList<ProvidedRate>> GetRatesAsync(
            string baseCurrency,
            IReadOnlyCollection<string> quoteCurrencies,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProvidedRate>>([]);
    }
}
