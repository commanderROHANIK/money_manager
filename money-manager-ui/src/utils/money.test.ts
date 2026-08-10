/**
 * These encode the product's money rules, not just the functions' behaviour.
 *
 * The one that matters most is the mixed-currency case: summing across currencies without an
 * exchange rate produces a confident wrong number, so `sumSameCurrency` reports `mixed` instead.
 * A refactor that starts quietly adding unlike amounts together should fail here.
 */
import { describe, it, expect } from 'vitest';
import { currentLocale, intlLocale } from '../i18n/locale';
import { formatMoney, sumSameCurrency } from './money';

/**
 * The expected digits, grouped the way the *application's* locale groups them.
 *
 * This used to read `toLocaleString(undefined, …)`, mirroring formatMoney, and the docblock said
 * that was deliberate so the output followed the user's own locale. That turned out to be the
 * defect rather than the design: `undefined` means "whatever this machine is set to", so one
 * deployment rendered `1 234 Ft` for one visitor and `1,234 Ft` for the next with nothing to
 * explain the difference — and the test could not see it, because it was reading the same
 * ambient setting the function was.
 *
 * Computed rather than hardcoded for the original reason, which still holds: a literal '240,000'
 * would only pass where the separator happens to be a comma, and Hungarian groups with a space.
 */
const localised = (value: number, options: Intl.NumberFormatOptions = {}) =>
  value.toLocaleString(intlLocale(), { maximumFractionDigits: 0, ...options });

describe('formatMoney', () => {
  it('renders an amount in its own currency', () => {
    expect(formatMoney(240000, 'HUF')).toContain(localised(240000));
    expect(formatMoney(1234.5, 'EUR')).toContain(localised(1234.5));
  });

  it('treats a missing amount as zero rather than blanking the widget', () => {
    // Distinct from the analytics rule that null means "cannot be known": by the time a value
    // reaches formatMoney the caller has already decided it is a number to display.
    expect(formatMoney(null, 'EUR')).toContain('0');
    expect(formatMoney(undefined, 'EUR')).toContain('0');
  });

  it('defaults to whole units and lets the caller opt back into decimals', () => {
    // Compared against locale-computed forms rather than a literal '.56', since the decimal
    // separator is a comma in much of Europe.
    expect(formatMoney(1234.56, 'EUR')).toContain(localised(1234.56));
    expect(formatMoney(1234.56, 'EUR', { maximumFractionDigits: 2 })).toContain(
      localised(1234.56, { maximumFractionDigits: 2 })
    );

    // And the two are genuinely different, which is what "defaults to whole units" means.
    expect(formatMoney(1234.56, 'EUR')).not.toEqual(
      formatMoney(1234.56, 'EUR', { maximumFractionDigits: 2 })
    );
  });

  it('upcases the currency code', () => {
    expect(formatMoney(100, 'eur')).toEqual(formatMoney(100, 'EUR'));
  });

  it('falls back to EUR when no currency is supplied', () => {
    expect(formatMoney(100, null)).toEqual(formatMoney(100, 'EUR'));
    expect(formatMoney(100, undefined)).toEqual(formatMoney(100, 'EUR'));
  });

  it('degrades to plain text rather than throwing on an unusable code', () => {
    // 'ABCD' is not three letters, so Intl rejects it. Note that a well-formed but unassigned
    // code like 'XYZ' does *not* throw — it formats fine — so this test would silently stop
    // exercising the catch block if it used one of those.
    const formatted = formatMoney(1500, 'ABCD');

    // The fallback calls toLocaleString(locale) with no options, so match that exactly.
    expect(formatted).toContain((1500).toLocaleString(intlLocale()));
    expect(formatted).toContain('ABCD');
  });

  it('formats in the application locale, not the reader\'s browser', () => {
    // The guarantee the previous version of this file could not make. Two visitors on the same
    // deployment must see the same figure whatever their machines are set to, and switching the
    // app's language is the only thing that changes it.
    //
    // Hungarian groups thousands with a space and writes decimals with a comma; en-GB does
    // neither. Asserting they differ pins that the locale is actually reaching Intl — a
    // formatter that ignored it would return identical strings and pass a weaker test.
    currentLocale.value = 'hu';
    const hungarian = formatMoney(1234567.5, 'HUF', { maximumFractionDigits: 2 });

    currentLocale.value = 'en';
    const english = formatMoney(1234567.5, 'HUF', { maximumFractionDigits: 2 });

    currentLocale.value = 'hu';

    expect(hungarian).not.toEqual(english);
    expect(english).toContain('1,234,567');
  });
});

describe('sumSameCurrency', () => {
  const amount = (x: { amount: number; currency: string }) => x.amount;
  const currency = (x: { amount: number; currency: string }) => x.currency;

  it('totals a single-currency list and reports the currency it used', () => {
    const result = sumSameCurrency(
      [
        { amount: 100, currency: 'HUF' },
        { amount: 250, currency: 'HUF' },
      ],
      amount,
      currency
    );

    expect(result).toEqual({ total: 350, currency: 'HUF', mixed: false });
  });

  it('refuses to add across currencies and says so', () => {
    // The point of the whole function. `mixed` is what lets the caller print "mixed currencies"
    // instead of a plausible, wrong, unlabelled number.
    const result = sumSameCurrency(
      [
        { amount: 100, currency: 'HUF' },
        { amount: 250, currency: 'EUR' },
      ],
      amount,
      currency
    );

    expect(result.mixed).toBe(true);
  });

  it('normalises case before deciding whether currencies match', () => {
    const result = sumSameCurrency(
      [
        { amount: 100, currency: 'huf' },
        { amount: 250, currency: 'HUF' },
      ],
      amount,
      currency
    );

    expect(result.mixed).toBe(false);
    expect(result.currency).toBe('HUF');
  });

  it('returns a zero total for an empty list without claiming a currency is mixed', () => {
    expect(sumSameCurrency([], amount, currency)).toEqual({
      total: 0,
      currency: 'EUR',
      mixed: false,
    });
  });

  it('coerces a missing amount to zero instead of producing NaN', () => {
    const result = sumSameCurrency(
      [
        { amount: 100, currency: 'EUR' },
        { amount: undefined as unknown as number, currency: 'EUR' },
      ],
      amount,
      currency
    );

    expect(result.total).toBe(100);
    expect(Number.isNaN(result.total)).toBe(false);
  });
});
