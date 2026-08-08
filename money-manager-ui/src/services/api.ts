import axios from 'axios';
import type { UpcomingEvent } from '../models/models';
import type { BankAccount, BankBalanceSummary } from '../models/models';
import type { Loan } from '../models/models';
import type { RentalProperty } from '../models/models';
import type { Stock } from '../models/models';

export const TOKEN_STORAGE_KEY = 'token';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

// Every data endpoint now requires a bearer token. Attaching it here rather than at each
// call site is what makes that workable — previously the header helper existed but nothing
// ever called it, so no request carried a token at all.
api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// A rejected or expired token should land the user back on the login screen rather than
// leaving the page silently empty. The redirect is a hard navigation on purpose: importing
// the router here would create a cycle (router -> components -> api -> router).
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      if (window.location.pathname !== '/login') {
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  }
);

export async function fetchUpcomingEvents(): Promise<UpcomingEvent[]> {
    const response = await api.get<UpcomingEvent[]>('/UpcomingEvents');
    return response.data;
}

export async function updateUpcomingEvent(id: number, updatedEvent: UpcomingEvent): Promise<void> {
    try {
        await api.put(`/UpcomingEvents/${id}`, updatedEvent);
    } catch (error) {
        console.error('Failed to update event:', error);
        throw new Error('Failed to update event');
    }
}

export async function deleteUpcomingEvent(id: number): Promise<void> {
    try {
      await api.delete(`/UpcomingEvents/${id}`);
    } catch (error) {
      console.error('Failed to delete event:', error);
      throw new Error('Failed to delete event');
    }
  }

  export async function createUpcomingEvent(newEvent: UpcomingEvent): Promise<UpcomingEvent> {
    const response = await api.post<UpcomingEvent>('/UpcomingEvents', newEvent);
    return response.data;
  }

  export async function fetchBankAccounts(): Promise<BankAccount[]> {
    const response = await api.get<BankAccount[]>('/BankAccounts');
    return response.data;
  }

  /**
   * Returns the whole summary rather than a bare number, because a bare number could not say
   * what currency it was in. The endpoint used to add balances across currencies and report the
   * result as if it meant something; now the caller gets the per-currency breakdown, the unit
   * the headline figure is in, and a null headline when no rate could produce one.
   */
  export async function fetchBankAccountsTotalBalance(): Promise<BankBalanceSummary> {
    const response = await api.get<BankBalanceSummary>('/BankAccounts/summary/total-balance');
    return response.data;
  }

  export async function createBankAccount(newBankAccount: BankAccount): Promise<BankAccount> {
    const response = await api.post<BankAccount>('/BankAccounts', newBankAccount);
    return response.data;
  }

  export async function updateBankAccount(id: number, updatedBankAccount: BankAccount): Promise<void> {
    await api.put(`/BankAccounts/${id}`, updatedBankAccount);
  }

  export async function deleteBankAccount(id: number): Promise<void> {
    await api.delete(`/BankAccounts/${id}`);
  }

  export async function fetchLoans(): Promise<Loan[]> {
    const response = await api.get<Loan[]>('/Loans');
    return response.data;
  }

  export async function createLoan(newLoan: Loan): Promise<Loan> {
    const response = await api.post<Loan>('/Loans', newLoan);
    return response.data;
  }

  export async function updateLoan(id: number, updatedLoan: Loan): Promise<void> {
    await api.put(`/Loans/${id}`, updatedLoan);
  }

  export async function deleteLoan(id: number): Promise<void> {
    await api.delete(`/Loans/${id}`);
  }

  export async function fetchRentalProperties(): Promise<RentalProperty[]> {
    const response = await api.get<RentalProperty[]>('/RentalProperties');
    return response.data;
  }

  export async function createRentalProperty(newProp: RentalProperty): Promise<RentalProperty> {
    const response = await api.post<RentalProperty>('/RentalProperties', newProp);
    return response.data;
  }

  export async function updateRentalProperty(id: number, prop: RentalProperty): Promise<void> {
    await api.put(`/RentalProperties/${id}`, prop);
  }

  export async function deleteRentalProperty(id: number): Promise<void> {
    await api.delete(`/RentalProperties/${id}`);
  }

  export async function fetchStocks(): Promise<Stock[]> {
    const response = await api.get<Stock[]>('/Stocks');
    return response.data;
  }

  export async function createStock(stock: Stock): Promise<Stock> {
    const response = await api.post<Stock>('/Stocks', stock);
    return response.data;
  }

  export async function updateStock(id: number, stock: Stock): Promise<void> {
    await api.put(`/Stocks/${id}`, stock);
  }

  export async function deleteStock(id: number): Promise<void> {
    await api.delete(`/Stocks/${id}`);
  }
