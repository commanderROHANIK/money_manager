/**
 * These encode the product's money rules, not just the functions' behaviour.
 *
 * The one that matters most is the mixed-currency case: summing across currencies without an
 * exchange rate produces a confident wrong number, so `sumSameCurrency` reports `mixed` instead.
 * A refactor that starts quietly adding unlike amounts together should fail here.
 */
import { describe, it, expect } from 'vitest';
import { formatMoney, sumSameCurrency } from './money';

/**
 * The expected digits, grouped the way the ambient locale groups them.
 *
 * formatMoney deliberately passes `undefined` as the locale so it follows the user's, which
 * means a hardcoded '240,000' only holds where the separator happens to be a comma. Asserting
 * a computed expectation keeps the test about the function instead of about the machine it
 * runs on — under de-DE the same call produces '240.000'.
 */
const localised = (value: number, options: Intl.NumberFormatOptions = {}) =>
  value.toLocaleString(undefined, { maximumFractionDigits: 0, ...options });

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

    // The fallback calls toLocaleString() with no options, so match that exactly.
    expect(formatted).toContain((1500).toLocaleString());
    expect(formatted).toContain('ABCD');
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
