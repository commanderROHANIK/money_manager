import { api } from './api';
import type { CurrentUser, ExchangeRate } from '../models/models';

export async function fetchExchangeRates(): Promise<ExchangeRate[]> {
  const response = await api.get<ExchangeRate[]>('/exchange-rates');
  return response.data;
}

/** Upserts the rate for a pair on a date — safe to call repeatedly with a correction. */
export async function saveExchangeRate(
  fromCurrency: string,
  toCurrency: string,
  rate: number,
  asOf?: string
): Promise<ExchangeRate> {
  const response = await api.put<ExchangeRate>('/exchange-rates', {
    fromCurrency,
    toCurrency,
    rate,
    asOf,
  });
  return response.data;
}

export async function deleteExchangeRate(id: number): Promise<void> {
  await api.delete(`/exchange-rates/${id}`);
}

export async function updateBaseCurrency(baseCurrency: string): Promise<CurrentUser> {
  const response = await api.put<CurrentUser>('/auth/me', { baseCurrency });
  return response.data;
}
