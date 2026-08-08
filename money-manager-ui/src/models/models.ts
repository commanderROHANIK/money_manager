export interface BankAccount {
    id: number;
    accountName: string;
    balance: number;
    bankName: string;
    accountNumber: string;
    accountType: string; // e.g. "Checking", "Savings"
    currencyCode: string; // ISO 4217
}

export interface Loan {
    id: number;
    loanName: string;
    loanAmount: number;
    remainingBalance: number;
    interestRate: number;
    dueDate: string; // ISO string format (DateTime in C#)
    isPaidOff: boolean;
    currencyCode: string;
    loanType?: LoanType;
    rentalPropertyId?: number | null;
    monthlyPayment?: number | null;
    startDate?: string | null;
    termMonths?: number | null;
}

export enum LoanType {
    Mortgage = 0,
    Personal = 1,
    Other = 2,
}

export interface Stock {
    id: number;
    ticker: string;
    sharesOwned: number;
    purchasePrice: number;
    currentPrice: number;
    purchaseDate: string; // ISO date string
    currencyCode: string;
}

export enum PropertyType {
    Apartment = 0,
    House = 1,
    Room = 2,
    Commercial = 3,
    Land = 4,
}

export enum PropertyStatus {
    Active = 0,
    Renovating = 1,
    Sold = 2,
}

/**
 * `isRented`, `rentAmount` and `rentDueDate` are computed by the server from the tenancy
 * running today rather than stored, so they cannot go stale.
 */
export interface RentalProperty {
    id: number;
    propertyName: string;
    address: string;
    city?: string | null;
    postalCode?: string | null;
    countryCode?: string | null;
    propertyType: PropertyType;
    sizeSqm?: number | null;
    bedrooms?: number | null;
    purchasePrice?: number | null;
    purchaseDate?: string | null;
    status: PropertyStatus;
    salePrice?: number | null;
    saleDate?: string | null;
    notes?: string | null;
    currencyCode: string;
    isRented: boolean;
    rentAmount: number;
    rentDueDate?: string | null;
    tenantName?: string | null;
}

export enum TransactionCategory {
    RentIncome = 0,
    DepositReceived = 1,
    OtherIncome = 2,
    Insurance = 10,
    PropertyTax = 11,
    Maintenance = 12,
    Repairs = 13,
    ManagementFee = 14,
    Utilities = 15,
    ServiceCharge = 16,
    OtherExpense = 17,
    MortgagePayment = 30,
    AcquisitionCost = 50,
    CapitalImprovement = 51,
}

export enum CashFlowDirection {
    Income = 0,
    Expense = 1,
}

export interface PropertyTransaction {
    id: number;
    rentalPropertyId: number;
    leaseId?: number | null;
    date: string;
    amount: number;
    currencyCode: string;
    category: TransactionCategory;
    description: string;
}

export interface Lease {
    id: number;
    rentalPropertyId: number;
    tenantName: string;
    tenantEmail?: string | null;
    tenantPhone?: string | null;
    startDate: string;
    endDate?: string | null;
    monthlyRent: number;
    currencyCode: string;
    rentDueDayOfMonth: number;
    depositAmount?: number | null;
    notes?: string | null;
}

export enum RentPriceSource {
    Contracted = 0,
    MarketEstimate = 1,
}

export interface RentPricePoint {
    id: number;
    rentalPropertyId: number;
    leaseId?: number | null;
    effectiveFrom: string;
    amount: number;
    currencyCode: string;
    source: RentPriceSource;
    providerKey?: string | null;
    notes?: string | null;
}

export enum ValuationSource {
    PurchasePrice = 0,
    OwnerEstimate = 1,
    Appraisal = 2,
    AutomatedModel = 3,
}

export interface PropertyValuation {
    id: number;
    rentalPropertyId: number;
    valuedOn: string;
    value: number;
    currencyCode: string;
    source: ValuationSource;
    notes?: string | null;
}

export enum PropertyEventType {
    Purchase = 0,
    TenantMovedIn = 1,
    TenantMovedOut = 2,
    RentChanged = 3,
    Maintenance = 4,
    Inspection = 5,
    Renovation = 6,
    Valuation = 7,
    MortgageLinked = 8,
    Sale = 9,
    Note = 10,
}

export interface PropertyEvent {
    id: number;
    rentalPropertyId: number;
    occurredOn: string;
    type: PropertyEventType;
    title: string;
    description?: string | null;
    isSystemGenerated: boolean;
}

export interface MetricWarning {
    code: string;
    message: string;
}

/** Every field is nullable: null means "cannot be determined", never zero. */
export interface PropertyMetrics {
    propertyId: number;
    propertyName: string;
    currencyCode: string;
    asOf: string;
    totalInvested: number | null;
    cashInvested: number | null;
    annualRentalIncome: number | null;
    annualOperatingExpenses: number | null;
    netOperatingIncome: number | null;
    annualDebtService: number | null;
    monthlyCashFlow: number | null;
    grossYield: number | null;
    netYield: number | null;
    capRate: number | null;
    cashOnCashReturn: number | null;
    currentValue: number | null;
    equity: number | null;
    appreciation: number | null;
    appreciationPercent: number | null;
    cumulativeNetCashFlow: number | null;
    totalReturn: number | null;
    totalRoi: number | null;
    annualizedRoi: number | null;
    yearsHeld: number | null;
    occupancyRate: number | null;
    marketMonthlyRent: number | null;
    contractedMonthlyRent: number | null;
    rentGapPercent: number | null;
    annualRentUplift: number | null;
    warnings: MetricWarning[];
}

/** A pair the server could not convert, so the UI can name the rate that is missing. */
export interface CurrencyPair {
    from: string;
    to: string;
}

/**
 * A rate a total was actually built with. `asOf` is null only for the identity conversion, and
 * `inverted` means it was read backwards off a row entered the other way round.
 */
export interface AppliedRate {
    from: string;
    to: string;
    rate: number;
    asOf: string | null;
    inverted: boolean;
}

/**
 * `currency` always names the unit the `total*` figures are in — the shared currency when the
 * portfolio has one, the owner's base currency when they had to be converted to exist at all.
 *
 * `mixedCurrency` says the portfolio spans currencies; `converted` says a rate was applied.
 * They answer different questions: the first is why conversion was needed, the second is what
 * has to be shown next to the number.
 */
export interface PortfolioAnalytics {
    properties: PropertyMetrics[];
    propertyCount: number;
    currency: string | null;
    mixedCurrency: boolean;
    totalInvested: number | null;
    totalCurrentValue: number | null;
    totalEquity: number | null;
    totalMonthlyCashFlow: number | null;
    totalAnnualRentUplift: number | null;
    portfolioRoi: number | null;
    baseCurrency: string;
    converted: boolean;
    missingRates: CurrencyPair[];
    appliedRates: AppliedRate[];
    warnings: MetricWarning[];
}

/** What is held in one currency. Always exact — no rate is involved in a subtotal. */
export interface CurrencyTotal {
    currencyCode: string;
    total: number;
}

/**
 * `byCurrency` is the part that is always true. When a rate is missing `totalBalance` is null
 * rather than approximate, and the breakdown still says exactly what is held.
 */
export interface BankBalanceSummary {
    totalBalance: number | null;
    currency: string;
    mixedCurrency: boolean;
    converted: boolean;
    baseCurrency: string;
    byCurrency: CurrencyTotal[];
    missingRates: CurrencyPair[];
    appliedRates: AppliedRate[];
    warnings: MetricWarning[];
}

export enum ExchangeRateSource {
    Manual = 0,
}

/** `rate` reads as "one baseCurrency buys this many quoteCurrency". */
export interface ExchangeRate {
    id: number;
    baseCurrency: string;
    quoteCurrency: string;
    rate: number;
    asOf: string;
    source: ExchangeRateSource;
}

export interface Settings {
    baseCurrency: string;
    alwaysConvertToBaseCurrency: boolean;
}

export interface UpcomingEvent {
    id: number;
    title: string;
    description: string;
    eventDate: string; // ISO date string
    isRecurring: boolean;
    isNotified: boolean;
    rentalPropertyId?: number | null;
    loanId?: number | null;
}
