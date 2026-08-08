namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// Converts between currencies using only rates that are actually on record.
    ///
    /// <para>
    /// The contract that matters: an unknown pair returns <c>null</c>. Never zero, never a 1.0
    /// "close enough" fallback. A total that silently treats 62,000,000 HUF as 62,000,000 EUR
    /// is worse than no total at all, because the user has no way to tell it happened.
    /// </para>
    /// </summary>
    public interface ICurrencyConverter
    {
        /// <summary>
        /// The rate that would be applied, or null when nothing on record covers the pair.
        /// Same-currency conversion yields a rate of 1 with a null <see cref="AppliedRate.AsOf"/>.
        /// </summary>
        AppliedRate? RateFor(CurrencyPair pair);

        /// <summary>
        /// <paramref name="amount"/> expressed in <c>pair.To</c>, or null when the pair has no
        /// rate. Deliberately unrounded: rounding each contribution before summing them drifts,
        /// so callers round the total once, at the end.
        /// </summary>
        decimal? Convert(decimal amount, CurrencyPair pair);
    }
}
