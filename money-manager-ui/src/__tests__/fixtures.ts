import {
  PropertyType, PropertyStatus, TransactionCategory, RentPriceSource,
  ValuationSource, PropertyEventType, LoanType,
} from '../models/models';

const iso = (d: string) => new Date(d).toISOString();

export const bankAccounts = [
  { id: 1, accountName: 'Everyday', balance: 412350, bankName: 'OTP', accountNumber: '1', accountType: 'Checking', currencyCode: 'HUF' },
  { id: 2, accountName: 'Rainy day', balance: 1850000, bankName: 'OTP', accountNumber: '2', accountType: 'Savings', currencyCode: 'HUF' },
  { id: 3, accountName: 'Travel', balance: 96000, bankName: 'Revolut', accountNumber: '3', accountType: 'Checking', currencyCode: 'HUF' },
];

export const loans = [
  { id: 1, loanName: 'Flat mortgage', loanAmount: 32000000, remainingBalance: 24800000, interestRate: 4.75, dueDate: iso('2041-06-01'), isPaidOff: false, currencyCode: 'HUF', loanType: LoanType.Mortgage },
  { id: 2, loanName: 'Car loan', loanAmount: 4200000, remainingBalance: 1150000, interestRate: 8.2, dueDate: iso('2027-03-15'), isPaidOff: false, currencyCode: 'HUF', loanType: LoanType.Personal },
  { id: 3, loanName: 'Student loan', loanAmount: 1800000, remainingBalance: 0, interestRate: 2.1, dueDate: iso('2024-09-01'), isPaidOff: true, currencyCode: 'HUF', loanType: LoanType.Other },
];

export const stocks = [
  { id: 1, ticker: 'MSFT', sharesOwned: 12, purchasePrice: 128000, currentPrice: 164500, purchaseDate: iso('2023-02-11'), currencyCode: 'HUF' },
  { id: 2, ticker: 'OTP', sharesOwned: 40, purchasePrice: 15200, currentPrice: 21850, purchaseDate: iso('2022-11-02'), currencyCode: 'HUF' },
  { id: 3, ticker: 'RIO', sharesOwned: 8, purchasePrice: 24900, currentPrice: 19100, purchaseDate: iso('2024-05-20'), currencyCode: 'HUF' },
  { id: 4, ticker: 'VWCE', sharesOwned: 55, purchasePrice: 38400, currentPrice: 44200, purchaseDate: iso('2021-08-14'), currencyCode: 'HUF' },
];

export const properties = [
  { id: 1, propertyName: 'Bartók flat', address: 'Bartók Béla út 42', city: 'Budapest', propertyType: PropertyType.Apartment, sizeSqm: 58, bedrooms: 2, purchasePrice: 62000000, purchaseDate: iso('2020-04-01'), status: PropertyStatus.Active, currencyCode: 'HUF', isRented: true, rentAmount: 240000, rentDueDate: iso('2026-08-10'), tenantName: 'Kovács Anna' },
  { id: 2, propertyName: 'Szeged studio', address: 'Kárász utca 9', city: 'Szeged', propertyType: PropertyType.Apartment, sizeSqm: 31, bedrooms: 1, purchasePrice: 28500000, purchaseDate: iso('2022-09-15'), status: PropertyStatus.Active, currencyCode: 'HUF', isRented: false, rentAmount: 0, rentDueDate: null, tenantName: null },
  { id: 3, propertyName: 'Lakeside house', address: 'Tó utca 3', city: 'Balatonfüred', propertyType: PropertyType.House, sizeSqm: 124, bedrooms: 4, purchasePrice: 98000000, purchaseDate: iso('2018-06-20'), status: PropertyStatus.Active, currencyCode: 'HUF', isRented: true, rentAmount: 410000, rentDueDate: iso('2026-08-05'), tenantName: 'Nagy Péter' },
];

const metric = (over: Record<string, unknown>) => ({
  propertyId: 1, propertyName: 'Bartók flat', currencyCode: 'HUF', asOf: iso('2026-08-01'),
  totalInvested: 62000000, cashInvested: 18600000, annualRentalIncome: 2880000,
  annualOperatingExpenses: 620000, netOperatingIncome: 2260000, annualDebtService: 1740000,
  monthlyCashFlow: 43333, grossYield: 0.046, netYield: 0.036, capRate: 0.036, cashOnCashReturn: 0.028,
  currentValue: 74000000, equity: 49200000, appreciation: 12000000, appreciationPercent: 0.194,
  cumulativeNetCashFlow: 1840000, totalReturn: 13840000, totalRoi: 0.223, annualizedRoi: 0.034,
  yearsHeld: 6.3, occupancyRate: 0.94, marketMonthlyRent: 285000, contractedMonthlyRent: 240000,
  rentGapPercent: 0.158, annualRentUplift: 540000, warnings: [],
  ...over,
});

export const propertyMetrics = metric({});
export const propertyMetricsAtMarket = metric({
  propertyId: 3, propertyName: 'Lakeside house', marketMonthlyRent: 390000,
  contractedMonthlyRent: 410000, rentGapPercent: -0.051, annualRentUplift: 0,
});

export const portfolio = {
  properties: [propertyMetrics, metric({ propertyId: 2, propertyName: 'Szeged studio', rentGapPercent: 0.224, annualRentUplift: 312000 }), propertyMetricsAtMarket],
  propertyCount: 3, currency: 'HUF', mixedCurrency: false,
  totalInvested: 188500000, totalCurrentValue: 231000000, totalEquity: 142400000,
  totalMonthlyCashFlow: 128000, totalAnnualRentUplift: 852000, portfolioRoi: 0.187,
};

export const transactions = [
  { id: 1, rentalPropertyId: 1, date: iso('2026-07-10'), amount: 240000, currencyCode: 'HUF', category: TransactionCategory.RentIncome, description: 'July rent' },
  { id: 2, rentalPropertyId: 1, date: iso('2026-07-02'), amount: 18400, currencyCode: 'HUF', category: TransactionCategory.Insurance, description: 'Building insurance' },
  { id: 3, rentalPropertyId: 1, date: iso('2026-06-21'), amount: 96500, currencyCode: 'HUF', category: TransactionCategory.Repairs, description: 'Boiler replacement' },
];

export const leases = [
  { id: 1, rentalPropertyId: 1, tenantName: 'Kovács Anna', startDate: iso('2025-03-01'), endDate: null, monthlyRent: 240000, currencyCode: 'HUF', rentDueDayOfMonth: 10 },
  { id: 2, rentalPropertyId: 1, tenantName: 'Tóth Gábor', startDate: iso('2022-05-01'), endDate: iso('2025-01-31'), monthlyRent: 195000, currencyCode: 'HUF', rentDueDayOfMonth: 5 },
];

export const rentHistory = [
  { id: 1, rentalPropertyId: 1, effectiveFrom: iso('2022-05-01'), amount: 195000, currencyCode: 'HUF', source: RentPriceSource.Contracted },
  { id: 2, rentalPropertyId: 1, effectiveFrom: iso('2023-06-01'), amount: 205000, currencyCode: 'HUF', source: RentPriceSource.MarketEstimate },
  { id: 3, rentalPropertyId: 1, effectiveFrom: iso('2025-03-01'), amount: 240000, currencyCode: 'HUF', source: RentPriceSource.Contracted },
  { id: 4, rentalPropertyId: 1, effectiveFrom: iso('2026-01-01'), amount: 285000, currencyCode: 'HUF', source: RentPriceSource.MarketEstimate },
];

export const valuations = [
  { id: 1, rentalPropertyId: 1, valuedOn: iso('2024-01-15'), value: 68000000, currencyCode: 'HUF', source: ValuationSource.OwnerEstimate },
  { id: 2, rentalPropertyId: 1, valuedOn: iso('2026-02-01'), value: 74000000, currencyCode: 'HUF', source: ValuationSource.Appraisal },
];

export const propertyEvents = [
  { id: 1, rentalPropertyId: 1, occurredOn: iso('2020-04-01'), type: PropertyEventType.Purchase, title: 'Purchased', description: 'Completed', isSystemGenerated: true },
  { id: 2, rentalPropertyId: 1, occurredOn: iso('2025-03-01'), type: PropertyEventType.TenantMovedIn, title: 'Kovács Anna moved in', description: null, isSystemGenerated: true },
  { id: 3, rentalPropertyId: 1, occurredOn: iso('2026-06-21'), type: PropertyEventType.Maintenance, title: 'Boiler replaced', description: 'Full replacement', isSystemGenerated: false },
];

export const upcomingEvents = [
  { id: 1, title: 'Insurance renewal', description: 'Building policy', eventDate: iso('2026-09-12'), isRecurring: true, isNotified: false },
  { id: 2, title: 'Mortgage rate review', description: 'Fixed period ends', eventDate: iso('2026-11-01'), isRecurring: false, isNotified: true },
  { id: 3, title: 'Gas safety check', description: 'Annual inspection', eventDate: iso('2025-02-20'), isRecurring: true, isNotified: true },
];
