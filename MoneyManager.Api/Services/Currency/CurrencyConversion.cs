using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// A directed pair, named <c>From</c>/<c>To</c> rather than base/quote on purpose.
    ///
    /// <para>
    /// The stored entity uses the FX convention (<c>BaseCurrency</c>/<c>QuoteCurrency</c>), but
    /// the arithmetic below is where getting the direction backwards produces a number that is
    /// wrong by a factor of 400 while still looking like money. Everything that multiplies goes
    /// through this type, so the direction is spelled out at the call site instead of being
    /// carried by argument order.
    /// </para>
    /// </summary>
    public readonly record struct CurrencyPair(string From, string To);

    /// <summary>
    /// A rate row, stripped of persistence concerns — the same trick
    /// <see cref="Analytics.PropertyAnalyticsInput"/> uses, and for the same reason: it lets
    /// every conversion rule be tested with a literal and no database.
    ///
    /// <para><c>Rate</c> is how many <c>QuoteCurrency</c> one <c>BaseCurrency</c> buys.</para>
    /// </summary>
    public readonly record struct ExchangeRateSnapshot(
        string BaseCurrency,
        string QuoteCurrency,
        decimal Rate,
        DateTime AsOf,
        ExchangeRateSource Source = ExchangeRateSource.Manual);

    /// <summary>
    /// The rate a conversion actually used, so the UI can show its working: "converted at
    /// 0.0025, recorded 1 July". <c>Inverted</c> is true when the figure was derived by
    /// reciprocal from a row entered the other way round.
    ///
    /// <para>
    /// <c>AsOf</c> and <c>Source</c> are both null only for the identity conversion, which no
    /// stored row backs. Everywhere else <c>Source</c> is what lets a total say where its rate
    /// came from — and it travels with the rate that was actually applied rather than being
    /// looked up again later, so the figure disclosed is always the figure used.
    /// </para>
    /// </summary>
    public sealed record AppliedRate(
        string From,
        string To,
        decimal Rate,
        DateTime? AsOf,
        bool Inverted,
        ExchangeRateSource? Source = null);
}
