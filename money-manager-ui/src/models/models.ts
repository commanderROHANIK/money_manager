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
}

export interface RentalProperty {
    id: number;
    propertyName: string;
    address: string;
    rentAmount: number;
    rentDueDate: string; // ISO date string from the backend
    isRented: boolean;
    currencyCode: string;
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
