import { api } from './api';
import type { ExchangeRate } from '../models/models';

export async function fetchExchangeRates(): Promise<ExchangeRate[]> {
  const response = await api.get<ExchangeRate[]>('/ExchangeRates');
  return response.data;
}

/**
 * Records what one `baseCurrency` is worth in `quoteCurrency`, replacing whatever was on record
 * for that pair — in either direction, since both describe the same fact.
 */
export async function upsertExchangeRate(
  baseCurrency: string,
  quoteCurrency: string,
  rate: number,
  asOf?: string
): Promise<ExchangeRate> {
  const response = await api.put<ExchangeRate>(
    `/ExchangeRates/${baseCurrency}/${quoteCurrency}`,
    { rate, asOf }
  );
  return response.data;
}

export async function deleteExchangeRate(baseCurrency: string, quoteCurrency: string): Promise<void> {
  await api.delete(`/ExchangeRates/${baseCurrency}/${quoteCurrency}`);
}
