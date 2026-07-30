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
 * Totals a list only when every entry shares one currency.
 *
 * When they do not, `total` is null and `mixed` is true. Adding unlike currencies would
 * produce a number that looks authoritative and is meaningless, so the caller has to render
 * the ambiguity instead. Converted totals come from the server, which has the rates —
 * these client-side widgets deliberately do not guess.
 */
export function sumSameCurrency<T>(
  items: T[],
  amountOf: (item: T) => number,
  currencyOf: (item: T) => string
): { total: number | null; currency: string; mixed: boolean } {
  if (items.length === 0) {
    return { total: 0, currency: 'EUR', mixed: false };
  }

  const currencies = new Set(items.map((i) => (currencyOf(i) ?? 'EUR').toUpperCase()));

  if (currencies.size > 1) {
    return { total: null, currency: [...currencies].sort().join(', '), mixed: true };
  }

  return {
    total: items.reduce((sum, item) => sum + (amountOf(item) || 0), 0),
    currency: [...currencies][0],
    mixed: false,
  };
}
