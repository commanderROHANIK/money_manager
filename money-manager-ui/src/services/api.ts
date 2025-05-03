import axios from 'axios';
import type { UpcomingEvent } from '../models/models';
import type { BankAccount } from '../models/models';

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
  