/**
 * The completeness tests here are the valuable ones.
 *
 * Adding a member to one of these enums without adding its label compiles fine — `Record<E, string>`
 * is checked at the definition site, but the enum and the label map live in different files and
 * a numeric enum member added to only one of them still type-checks in the places that matter.
 * The visible symptom is `undefined` rendered in a dropdown, which nothing else catches.
 */
import { describe, it, expect, afterEach } from 'vitest';
import { currentLocale, DEFAULT_LOCALE } from '../i18n/locale';
import {
  PropertyEventType,
  PropertyStatus,
  PropertyType,
  RentPriceSource,
  TransactionCategory,
} from '../models/models';
import {
  PROPERTY_EVENT_LABELS,
  PROPERTY_STATUS_LABELS,
  PROPERTY_TYPE_LABELS,
  RENT_SOURCE_LABELS,
  TRANSACTION_CATEGORY_GROUPS,
  TRANSACTION_CATEGORY_LABELS,
  formatDate,
  formatPercent,
  isIncome,
} from './labels';

/** Numeric enums carry a reverse mapping, so the values include the member names too. */
const membersOf = (e: object): number[] => Object.values(e).filter((v) => typeof v === 'number');

describe('label completeness', () => {
  it.each([
    ['TransactionCategory', TransactionCategory, TRANSACTION_CATEGORY_LABELS.value],
    ['PropertyType', PropertyType, PROPERTY_TYPE_LABELS.value],
    ['PropertyStatus', PropertyStatus, PROPERTY_STATUS_LABELS.value],
    ['PropertyEventType', PropertyEventType, PROPERTY_EVENT_LABELS.value],
    ['RentPriceSource', RentPriceSource, RENT_SOURCE_LABELS.value],
  ])('every %s member has a label', (_name, enumObject, labels) => {
    for (const member of membersOf(enumObject)) {
      const label = (labels as Record<number, string>)[member];

      expect(label, `enum member ${member} has no label`).toBeDefined();
      expect(label.trim()).not.toBe('');
    }
  });
});

describe('TRANSACTION_CATEGORY_GROUPS', () => {
  const grouped = TRANSACTION_CATEGORY_GROUPS.value.flatMap((g) => g.categories);

  it('accounts for every category exactly once', () => {
    // A category missing from the groups is unreachable in the entry form even though it has a
    // label and the backend accepts it.
    expect([...grouped].sort()).toEqual([...membersOf(TransactionCategory)].sort());
    expect(new Set(grouped).size).toBe(grouped.length);
  });

  it('gives every group a non-empty label', () => {
    for (const group of TRANSACTION_CATEGORY_GROUPS.value) {
      expect(group.label.trim()).not.toBe('');
      expect(group.categories.length).toBeGreaterThan(0);
    }
  });
});

describe('isIncome', () => {
  it('classifies the three income categories as income', () => {
    expect(isIncome(TransactionCategory.RentIncome)).toBe(true);
    expect(isIncome(TransactionCategory.DepositReceived)).toBe(true);
    expect(isIncome(TransactionCategory.OtherIncome)).toBe(true);
  });

  it('classifies running costs, financing and capital as not income', () => {
    expect(isIncome(TransactionCategory.Repairs)).toBe(false);
    expect(isIncome(TransactionCategory.MortgagePayment)).toBe(false);
    expect(isIncome(TransactionCategory.CapitalImprovement)).toBe(false);
  });
});

// The locale is module state, so a test that switches it has to put it back — otherwise the
// next file to run in the same worker formats in whatever language this one finished in.
afterEach(() => {
  currentLocale.value = DEFAULT_LOCALE;
});

describe('formatPercent', () => {
  // These asserted '6.6%' outright before the application had a language. Hungarian writes a
  // decimal comma, so the expectations are now per-locale — the rounding and the digit count,
  // which is what the function is actually responsible for, are unchanged.
  it('renders a ratio as a percentage', () => {
    currentLocale.value = 'en';
    expect(formatPercent(0.0655)).toBe('6.6%');
    expect(formatPercent(0.0655, 2)).toBe('6.55%');
    expect(formatPercent(-0.051)).toBe('-5.1%');

    currentLocale.value = 'hu';
    expect(formatPercent(0.0655)).toBe('6,6%');
    expect(formatPercent(0.0655, 2)).toBe('6,55%');
  });

  it('renders an unknown value as a dash, never as zero', () => {
    // The product rule: null means "cannot be known". Rendering it as 0% would assert something
    // false about the property. The dash is not translated — it says the same thing in both
    // languages, and a translated stand-in would be one more place for the rule to get lost.
    for (const locale of ['hu', 'en'] as const) {
      currentLocale.value = locale;
      expect(formatPercent(null)).toBe('—');
      expect(formatPercent(undefined)).toBe('—');
    }
  });

  it('still renders a genuine zero as zero', () => {
    currentLocale.value = 'en';
    expect(formatPercent(0)).toBe('0.0%');

    currentLocale.value = 'hu';
    expect(formatPercent(0)).toBe('0,0%');
  });
});

describe('formatDate', () => {
  it('renders the date in the active locale', () => {
    // Was asserted as the bare ISO substring '2026-07-10', which is not how a Hungarian reads a
    // date. Both forms below are the same calendar day written two ways.
    currentLocale.value = 'hu';
    expect(formatDate('2026-07-10T00:00:00.000Z')).toBe('2026. 07. 10.');

    currentLocale.value = 'en';
    expect(formatDate('2026-07-10T00:00:00.000Z')).toBe('10/07/2026');
  });

  it('reads a date-only value and a timestamp as the same calendar day', () => {
    // Why formatDate takes the string apart instead of calling new Date(value): a bare
    // '2026-07-10' parses as midnight UTC, which is the 9th of July in New York. These are
    // calendar dates — a lease start, a purchase — and the day must survive being read in a
    // different timezone from the one it was entered in. The old ISO-substring implementation
    // could not get that wrong; reformatting through Intl can.
    //
    // Worth being straight about what this does and does not cover: CI runs in UTC, where a
    // new Date(value) implementation would agree with the correct one, so this cannot catch a
    // regression on its own. It pins the two input shapes against each other, and the timezone
    // guarantee itself rests on the parsing in formatDate rather than on this assertion.
    // Catching it properly would mean running the suite under a negative-offset TZ, which is a
    // process-wide setting and would change every other date test's meaning at the same time.
    for (const locale of ['hu', 'en'] as const) {
      currentLocale.value = locale;
      expect(formatDate('2026-07-10')).toContain('10');
      expect(formatDate('2026-07-10T00:00:00.000Z')).toEqual(formatDate('2026-07-10'));
      expect(formatDate('2026-01-01')).toContain('2026');
    }
  });

  it('renders a missing date as a dash', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
  });

  it('returns the date part unchanged rather than throwing on an unparseable value', () => {
    // A malformed value should not blank out the row it sits in.
    expect(formatDate('not-a-date')).toBe('not-a-date');
  });
});
