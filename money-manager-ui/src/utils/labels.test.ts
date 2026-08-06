/**
 * The completeness tests here are the valuable ones.
 *
 * Adding a member to one of these enums without adding its label compiles fine — `Record<E, string>`
 * is checked at the definition site, but the enum and the label map live in different files and
 * a numeric enum member added to only one of them still type-checks in the places that matter.
 * The visible symptom is `undefined` rendered in a dropdown, which nothing else catches.
 */
import { describe, it, expect } from 'vitest';
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
    ['TransactionCategory', TransactionCategory, TRANSACTION_CATEGORY_LABELS],
    ['PropertyType', PropertyType, PROPERTY_TYPE_LABELS],
    ['PropertyStatus', PropertyStatus, PROPERTY_STATUS_LABELS],
    ['PropertyEventType', PropertyEventType, PROPERTY_EVENT_LABELS],
    ['RentPriceSource', RentPriceSource, RENT_SOURCE_LABELS],
  ])('every %s member has a label', (_name, enumObject, labels) => {
    for (const member of membersOf(enumObject)) {
      const label = (labels as Record<number, string>)[member];

      expect(label, `enum member ${member} has no label`).toBeDefined();
      expect(label.trim()).not.toBe('');
    }
  });
});

describe('TRANSACTION_CATEGORY_GROUPS', () => {
  const grouped = TRANSACTION_CATEGORY_GROUPS.flatMap((g) => g.categories);

  it('accounts for every category exactly once', () => {
    // A category missing from the groups is unreachable in the entry form even though it has a
    // label and the backend accepts it.
    expect([...grouped].sort()).toEqual([...membersOf(TransactionCategory)].sort());
    expect(new Set(grouped).size).toBe(grouped.length);
  });

  it('gives every group a non-empty label', () => {
    for (const group of TRANSACTION_CATEGORY_GROUPS) {
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

describe('formatPercent', () => {
  it('renders a ratio as a percentage', () => {
    expect(formatPercent(0.0655)).toBe('6.6%');
    expect(formatPercent(0.0655, 2)).toBe('6.55%');
    expect(formatPercent(-0.051)).toBe('-5.1%');
  });

  it('renders an unknown value as a dash, never as zero', () => {
    // The product rule: null means "cannot be known". Rendering it as 0% would assert something
    // false about the property.
    expect(formatPercent(null)).toBe('—');
    expect(formatPercent(undefined)).toBe('—');
  });

  it('still renders a genuine zero as zero', () => {
    expect(formatPercent(0)).toBe('0.0%');
  });
});

describe('formatDate', () => {
  it('reduces an ISO timestamp to its date part', () => {
    expect(formatDate('2026-07-10T00:00:00.000Z')).toBe('2026-07-10');
  });

  it('renders a missing date as a dash', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('')).toBe('—');
  });
});
