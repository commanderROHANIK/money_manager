import { intlLocale } from '../i18n/locale';

/**
 * Single place that turns an amount plus its currency into display text.
 *
 * Before this existed the widgets each hardcoded a currency, so the same page could show
 * one figure in EUR, the next in USD and the next in HUF regardless of what the data
 * actually was. Always pass the record's own `currencyCode`.
 *
 * Formatted in the *application's* locale, not the browser's. This used to pass `undefined`,
 * which means "whatever this machine is set to" — so one deployment rendered `1 234 Ft` for one
 * visitor and `1,234 Ft` for the next, with no setting anywhere to explain the difference. That
 * is a separate defect from being untranslated, and it would have survived the translation work
 * untouched.
 */
export function formatMoney(
  amount: number | null | undefined,
  currency: string | null | undefined,
  options: Intl.NumberFormatOptions = {}
): string {
  const value = amount ?? 0;
  const code = (currency ?? 'EUR').toUpperCase();
  const locale = intlLocale();

  try {
    return value.toLocaleString(locale, {
      style: 'currency',
      currency: code,
      maximumFractionDigits: 0,
      ...options,
    });
  } catch {
    // An unrecognised ISO code should not blank out the whole widget.
    return `${value.toLocaleString(locale)} ${code}`;
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
