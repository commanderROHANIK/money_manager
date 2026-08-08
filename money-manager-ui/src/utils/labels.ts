import {
  PropertyEventType,
  PropertyStatus,
  PropertyType,
  RentPeriodStatus,
  RentPriceSource,
  TransactionCategory,
} from '../models/models';

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

/** A ratio from the API (0.0655) as a percentage string. Null stays visibly unknown. */
export function formatPercent(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined) return '—';
  return `${(value * 100).toFixed(digits)}%`;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  return value.split('T')[0];
}
