import axios from 'axios';
import type { UpcomingEvent } from '../models/models';
import type { BankAccount, BankBalanceSummary } from '../models/models';
import type { Loan } from '../models/models';
import type { RentalProperty } from '../models/models';
import type { Stock, StockValueSummary } from '../models/models';

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

/**
 * A failed write, in the shape a form can actually render.
 *
 * `fields` is keyed by the form's own field names so a component can look up
 * `errors.fields.accountName` directly. `message` carries the cases that belong to no single
 * field — a 409 conflict, a 500 — which is what a toast or a banner should show instead.
 */
export interface ApiError {
  fields: Record<string, string>;
  message: string | null;
  status: number | null;
}

/** ASP.NET names fields as they are declared (`AccountName`); the forms use `accountName`. */
function toFieldName(key: string): string {
  return key.charAt(0).toLowerCase() + key.slice(1);
}

/**
 * Turns whatever the API rejected a write with into one predictable object.
 *
 * The server answers every failure as RFC 7807 now: a validation failure carries an `errors` map
 * keyed by field, and everything else carries a `detail` string. This is the single place that
 * knows that, so a component never reaches into `err.response.data` and guesses.
 *
 * Defensive about the envelope on purpose — a network failure, a proxy's HTML error page, or a
 * response that predates this convention all have to come back as *something* a form can show,
 * rather than throwing a second error inside the error handler.
 */
export function extractApiError(error: unknown): ApiError {
  const empty: ApiError = { fields: {}, message: null, status: null };

  if (!axios.isAxiosError(error)) {
    return { ...empty, message: error instanceof Error ? error.message : 'Something went wrong.' };
  }

  const status = error.response?.status ?? null;
  const data = error.response?.data as
    | { errors?: Record<string, string[] | string>; detail?: string; title?: string }
    | undefined;

  if (!data || typeof data !== 'object') {
    return { fields: {}, message: error.message, status };
  }

  const fields: Record<string, string> = {};

  for (const [key, value] of Object.entries(data.errors ?? {})) {
    // Only the first message per field: a form shows one line under an input, and the rest are
    // almost always restatements of the same problem.
    const first = Array.isArray(value) ? value[0] : value;
    if (typeof first === 'string' && first.length > 0) {
      fields[toFieldName(key)] = first;
    }
  }

  // `title` is the last resort: on a validation failure it is the generic "One or more validation
  // errors occurred", which is worth showing only when nothing more specific came back.
  const message =
    data.detail ?? (Object.keys(fields).length > 0 ? null : (data.title ?? error.message));

  return { fields, message, status };
}

export async function fetchUpcomingEvents(): Promise<UpcomingEvent[]> {
    const response = await api.get<UpcomingEvent[]>('/UpcomingEvents');
    return response.data;
}

export async function updateUpcomingEvent(id: number, updatedEvent: UpcomingEvent): Promise<void> {
    try {
        await api.put(`/UpcomingEvents/${id}`, updatedEvent);
    } catch (error) {
        console.error('Failed to update event:', error);
        throw new Error('Failed to update event', { cause: error });
    }
}

export async function deleteUpcomingEvent(id: number): Promise<void> {
    try {
      await api.delete(`/UpcomingEvents/${id}`);
    } catch (error) {
      console.error('Failed to delete event:', error);
      throw new Error('Failed to delete event', { cause: error });
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

  /**
   * The stocks equivalent of `fetchBankAccountsTotalBalance`: the whole summary rather than a
   * bare number, since a bare number could not say what currency it was in. Sums
   * `sharesOwned * currentPrice` across currencies at the owner's own rates, and reports the
   * per-currency breakdown plus a null headline when no rate could produce one.
   */
  export async function fetchStocksTotalValue(): Promise<StockValueSummary> {
    const response = await api.get<StockValueSummary>('/Stocks/summary/total-value');
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
