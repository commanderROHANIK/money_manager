import { intlLocale } from '../i18n/locale';
import {
  PropertyEventType,
  PropertyStatus,
  PropertyType,
  RentPeriodStatus,
  RentPriceSource,
  TransactionCategory,
} from '../models/models';

/**
 * What a metric renders as when it cannot be known.
 *
 * Deliberately not translated, and deliberately not `0`. Every analytics figure is nullable
 * because null means "cannot be known from the ledger", and an em dash says that in every
 * language — whereas a zero would be a confident wrong number, which is the thing this product
 * exists not to produce.
 */
const UNKNOWN = '—';

export const TRANSACTION_CATEGORY_LABELS: Record<TransactionCategory, string> = {
  [TransactionCategory.RentIncome]: 'Rent received',
  [TransactionCategory.DepositReceived]: 'Deposit received',
  [TransactionCategory.OtherIncome]: 'Other income',
  [TransactionCategory.Insurance]: 'Insurance',
  [TransactionCategory.PropertyTax]: 'Property tax',
  [TransactionCategory.Maintenance]: 'Maintenance',
  [TransactionCategory.Repairs]: 'Repairs',
  [TransactionCategory.ManagementFee]: 'Management fee',
  [TransactionCategory.Utilities]: 'Utilities',
  [TransactionCategory.ServiceCharge]: 'Service charge',
  [TransactionCategory.OtherExpense]: 'Other expense',
  [TransactionCategory.MortgagePayment]: 'Mortgage payment',
  [TransactionCategory.AcquisitionCost]: 'Acquisition cost',
  [TransactionCategory.CapitalImprovement]: 'Capital improvement',
};

/** Grouped for the entry form so the list is navigable rather than one flat dropdown. */
export const TRANSACTION_CATEGORY_GROUPS: { label: string; categories: TransactionCategory[] }[] = [
  {
    label: 'Income',
    categories: [
      TransactionCategory.RentIncome,
      TransactionCategory.DepositReceived,
      TransactionCategory.OtherIncome,
    ],
  },
  {
    label: 'Running costs',
    categories: [
      TransactionCategory.Insurance,
      TransactionCategory.PropertyTax,
      TransactionCategory.Maintenance,
      TransactionCategory.Repairs,
      TransactionCategory.ManagementFee,
      TransactionCategory.Utilities,
      TransactionCategory.ServiceCharge,
      TransactionCategory.OtherExpense,
    ],
  },
  { label: 'Financing', categories: [TransactionCategory.MortgagePayment] },
  {
    label: 'Capital',
    categories: [TransactionCategory.AcquisitionCost, TransactionCategory.CapitalImprovement],
  },
];

const INCOME_CATEGORIES = new Set<TransactionCategory>([
  TransactionCategory.RentIncome,
  TransactionCategory.DepositReceived,
  TransactionCategory.OtherIncome,
]);

export function isIncome(category: TransactionCategory): boolean {
  return INCOME_CATEGORIES.has(category);
}

export const RENT_STATUS_LABELS: Record<RentPeriodStatus, string> = {
  [RentPeriodStatus.Vacant]: 'Vacant',
  [RentPeriodStatus.Unpaid]: 'Unpaid',
  [RentPeriodStatus.Partial]: 'Partial',
  [RentPeriodStatus.Paid]: 'Paid',
};

export const PROPERTY_TYPE_LABELS: Record<PropertyType, string> = {
  [PropertyType.Apartment]: 'Apartment',
  [PropertyType.House]: 'House',
  [PropertyType.Room]: 'Room',
  [PropertyType.Commercial]: 'Commercial',
  [PropertyType.Land]: 'Land',
};

export const PROPERTY_STATUS_LABELS: Record<PropertyStatus, string> = {
  [PropertyStatus.Active]: 'Active',
  [PropertyStatus.Renovating]: 'Renovating',
  [PropertyStatus.Sold]: 'Sold',
};

export const PROPERTY_EVENT_LABELS: Record<PropertyEventType, string> = {
  [PropertyEventType.Purchase]: 'Purchase',
  [PropertyEventType.TenantMovedIn]: 'Tenant moved in',
  [PropertyEventType.TenantMovedOut]: 'Tenant moved out',
  [PropertyEventType.RentChanged]: 'Rent changed',
  [PropertyEventType.Maintenance]: 'Maintenance',
  [PropertyEventType.Inspection]: 'Inspection',
  [PropertyEventType.Renovation]: 'Renovation',
  [PropertyEventType.Valuation]: 'Valuation',
  [PropertyEventType.MortgageLinked]: 'Mortgage linked',
  [PropertyEventType.Sale]: 'Sale',
  [PropertyEventType.Note]: 'Note',
};

export const RENT_SOURCE_LABELS: Record<RentPriceSource, string> = {
  [RentPriceSource.Contracted]: 'Charged',
  [RentPriceSource.MarketEstimate]: 'Market estimate',
};

/**
 * A ratio from the API (0.0655) as a percentage string. Null stays visibly unknown.
 *
 * Formatted in the application's locale, so Hungarian gets `6,6%` with the decimal comma it
 * writes numbers with rather than the `6.6%` a `toFixed` produces regardless of language.
 */
export function formatPercent(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined) return UNKNOWN;

  return `${(value * 100).toLocaleString(intlLocale(), {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  })}%`;
}

/**
 * A date from the API as display text — `2026. 08. 05.` in Hungarian, `05/08/2026` in English.
 *
 * The value is split on `T` and reassembled from its parts rather than handed to `new Date()`,
 * which would parse a bare `2026-08-05` as midnight UTC and render it as the 4th for anyone west
 * of Greenwich. The date this shows is a calendar date, not an instant, and it has to survive
 * being read in a different timezone from the one it was entered in.
 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return UNKNOWN;

  const [datePart] = value.split('T');
  const [year, month, day] = datePart.split('-').map(Number);

  if (!year || !month || !day) return datePart;

  return new Intl.DateTimeFormat(intlLocale(), {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(year, month - 1, day));
}
