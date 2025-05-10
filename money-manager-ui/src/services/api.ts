import axios from 'axios';
import type { UpcomingEvent } from '../models/models';
import type { BankAccount } from '../models/models';
import type { Loan } from '../models/models';
import type { RentalProperty } from '../models/models';
import type { Stock } from '../models/models';

export const api = axios.create({
  baseURL: 'http://localhost:5296/api', // adjust port if needed
});

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

  export async function fetchBankAccountsTotalBalance(): Promise<number | null> {
    const response = await api.get<number | null>('/BankAccounts/summary/total-balance');
    return response.data["totalBalance"] || null; 
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