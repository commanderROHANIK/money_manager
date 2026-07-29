/**
 * Single place that turns an amount plus its currency into display text.
 *
 * Before this existed the widgets each hardcoded a currency, so the same page could show
 * one figure in EUR, the next in USD and the next in HUF regardless of what the data
 * actually was. Always pass the record's own `currencyCode`.
 */
export function formatMoney(
  amount: number | null | undefined,
  currency: string | null | undefined,
  options: Intl.NumberFormatOptions = {}
): string {
  const value = amount ?? 0;
  const code = (currency ?? 'EUR').toUpperCase();

  try {
    return value.toLocaleString(undefined, {
      style: 'currency',
      currency: code,
      maximumFractionDigits: 0,
      ...options,
    });
  } catch {
    // An unrecognised ISO code should not blank out the whole widget.
    return `${value.toLocaleString()} ${code}`;
  }
}

/**
 * Totals a mixed-currency list only when every entry shares one currency, and reports the
 * currency it used. Summing across currencies is meaningless without an exchange rate, so
 * this returns `mixed` instead of a fabricated number and the caller can say so.
 */
export function sumSameCurrency<T>(
  items: T[],
  amountOf: (item: T) => number,
  currencyOf: (item: T) => string
): { total: number; currency: string; mixed: boolean } {
  if (items.length === 0) {
    return { total: 0, currency: 'EUR', mixed: false };
  }

  const currencies = new Set(items.map((i) => (currencyOf(i) ?? 'EUR').toUpperCase()));
  const total = items.reduce((sum, item) => sum + (amountOf(item) || 0), 0);

  return {
    total,
    currency: currencies.size === 1 ? [...currencies][0] : 'EUR',
    mixed: currencies.size > 1,
  };
}
