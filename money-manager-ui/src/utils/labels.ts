import { computed } from 'vue';
import { i18n } from '../i18n';
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

/**
 * The global translator, rather than `useI18n()`.
 *
 * These maps are module-level constants read from templates and from plain functions alike, so
 * there is no component instance to hang a composable off. `computed` is what keeps them honest:
 * `i18n.global.locale` is a ref, so each map recomputes when the language changes instead of
 * freezing whatever was current when this module first loaded.
 */
const t = (key: string): string => i18n.global.t(key);

export const TRANSACTION_CATEGORY_LABELS = computed<Record<TransactionCategory, string>>(() => ({
  [TransactionCategory.RentIncome]: t('transactionCategory.rentIncome'),
  [TransactionCategory.DepositReceived]: t('transactionCategory.depositReceived'),
  [TransactionCategory.OtherIncome]: t('transactionCategory.otherIncome'),
  [TransactionCategory.Insurance]: t('transactionCategory.insurance'),
  [TransactionCategory.PropertyTax]: t('transactionCategory.propertyTax'),
  [TransactionCategory.Maintenance]: t('transactionCategory.maintenance'),
  [TransactionCategory.Repairs]: t('transactionCategory.repairs'),
  [TransactionCategory.ManagementFee]: t('transactionCategory.managementFee'),
  [TransactionCategory.Utilities]: t('transactionCategory.utilities'),
  [TransactionCategory.ServiceCharge]: t('transactionCategory.serviceCharge'),
  [TransactionCategory.OtherExpense]: t('transactionCategory.otherExpense'),
  [TransactionCategory.MortgagePayment]: t('transactionCategory.mortgagePayment'),
  [TransactionCategory.AcquisitionCost]: t('transactionCategory.acquisitionCost'),
  [TransactionCategory.CapitalImprovement]: t('transactionCategory.capitalImprovement'),
}));

/** Grouped for the entry form so the list is navigable rather than one flat dropdown. */
export const TRANSACTION_CATEGORY_GROUPS = computed<
  { label: string; categories: TransactionCategory[] }[]
>(() => [
  {
    label: t('transactionGroup.income'),
    categories: [
      TransactionCategory.RentIncome,
      TransactionCategory.DepositReceived,
      TransactionCategory.OtherIncome,
    ],
  },
  {
    label: t('transactionGroup.runningCosts'),
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
  { label: t('transactionGroup.financing'), categories: [TransactionCategory.MortgagePayment] },
  {
    label: t('transactionGroup.capital'),
    categories: [TransactionCategory.AcquisitionCost, TransactionCategory.CapitalImprovement],
  },
]);

const INCOME_CATEGORIES = new Set<TransactionCategory>([
  TransactionCategory.RentIncome,
  TransactionCategory.DepositReceived,
  TransactionCategory.OtherIncome,
]);

export function isIncome(category: TransactionCategory): boolean {
  return INCOME_CATEGORIES.has(category);
}

export const RENT_STATUS_LABELS = computed<Record<RentPeriodStatus, string>>(() => ({
  [RentPeriodStatus.Vacant]: t('rentStatus.vacant'),
  [RentPeriodStatus.Unpaid]: t('rentStatus.unpaid'),
  [RentPeriodStatus.Partial]: t('rentStatus.partial'),
  [RentPeriodStatus.Paid]: t('rentStatus.paid'),
}));

export const PROPERTY_TYPE_LABELS = computed<Record<PropertyType, string>>(() => ({
  [PropertyType.Apartment]: t('propertyType.apartment'),
  [PropertyType.House]: t('propertyType.house'),
  [PropertyType.Room]: t('propertyType.room'),
  [PropertyType.Commercial]: t('propertyType.commercial'),
  [PropertyType.Land]: t('propertyType.land'),
}));

export const PROPERTY_STATUS_LABELS = computed<Record<PropertyStatus, string>>(() => ({
  [PropertyStatus.Active]: t('propertyStatus.active'),
  [PropertyStatus.Renovating]: t('propertyStatus.renovating'),
  [PropertyStatus.Sold]: t('propertyStatus.sold'),
}));

export const PROPERTY_EVENT_LABELS = computed<Record<PropertyEventType, string>>(() => ({
  [PropertyEventType.Purchase]: t('propertyEvent.purchase'),
  [PropertyEventType.TenantMovedIn]: t('propertyEvent.tenantMovedIn'),
  [PropertyEventType.TenantMovedOut]: t('propertyEvent.tenantMovedOut'),
  [PropertyEventType.RentChanged]: t('propertyEvent.rentChanged'),
  [PropertyEventType.Maintenance]: t('propertyEvent.maintenance'),
  [PropertyEventType.Inspection]: t('propertyEvent.inspection'),
  [PropertyEventType.Renovation]: t('propertyEvent.renovation'),
  [PropertyEventType.Valuation]: t('propertyEvent.valuation'),
  [PropertyEventType.MortgageLinked]: t('propertyEvent.mortgageLinked'),
  [PropertyEventType.Sale]: t('propertyEvent.sale'),
  [PropertyEventType.Note]: t('propertyEvent.note'),
}));

export const RENT_SOURCE_LABELS = computed<Record<RentPriceSource, string>>(() => ({
  [RentPriceSource.Contracted]: t('rentSource.contracted'),
  [RentPriceSource.MarketEstimate]: t('rentSource.marketEstimate'),
}));

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
