export interface UpcomingEvent {
    id: number;
    name: string;
    eventDate: string; // ISO string format
}

export interface BankAccount {
    id: number;
    name: string;
    bankName: string;
    balance: number;
    currency: string;
    accountType: 'checking' | 'savings' | 'credit' | 'other';
}

export interface Transaction {
    id: number;
    amount: number;
    date: string;
    description?: string;
    category: string;
    transactionType: 'income' | 'expense' | 'transfer';
    bankAccountId: number;
}

export interface Loan {
    id: number;
    name: string;
    principalAmount: number;
    outstandingAmount: number;
    monthlyPayment: number;
    interestRate: number;
    dueDay: number; // e.g., 15th of each month
    nextPaymentDate: string;
}

export interface RentalIncome {
    id: number;
    propertyName: string;
    amount: number;
    dueDay: number;
    receivedDate?: string;
}

export interface StockInvestment {
    id: number;
    symbol: string;
    companyName: string;
    sharesOwned: number;
    averageBuyPrice: number;
    currentPrice: number;
    purchaseDate: string;
}

export interface Notification {
    id: number;
    title: string;
    message: string;
    eventDate: string;
    isRead: boolean;
    createdAt: string;
}
