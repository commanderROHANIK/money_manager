/**
 * These encode the product's money rules, not just the functions' behaviour.
 *
 * The one that matters most is the mixed-currency case: summing across currencies without an
 * exchange rate produces a confident wrong number, so `sumSameCurrency` reports `mixed` instead.
 * A refactor that starts quietly adding unlike amounts together should fail here.
 */
import { describe, it, expect } from 'vitest';
import { formatMoney, sumSameCurrency } from './money';

describe('formatMoney', () => {
  it('renders an amount in its own currency', () => {
    expect(formatMoney(240000, 'HUF')).toContain('240,000');
    expect(formatMoney(1234.5, 'EUR')).toContain('1,235');
  });

  it('treats a missing amount as zero rather than blanking the widget', () => {
    // Distinct from the analytics rule that null means "cannot be known": by the time a value
    // reaches formatMoney the caller has already decided it is a number to display.
    expect(formatMoney(null, 'EUR')).toContain('0');
    expect(formatMoney(undefined, 'EUR')).toContain('0');
  });

  it('defaults to whole units and lets the caller opt back into decimals', () => {
    expect(formatMoney(1234.56, 'EUR')).not.toContain('.56');
    expect(formatMoney(1234.56, 'EUR', { maximumFractionDigits: 2 })).toContain('.56');
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

    expect(formatted).toContain('1,500');
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
