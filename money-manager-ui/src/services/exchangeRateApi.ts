import { api } from './api';
import type { ExchangeRate } from '../models/models';

export async function fetchExchangeRates(): Promise<ExchangeRate[]> {
  const response = await api.get<ExchangeRate[]>('/ExchangeRates');
  return response.data;
}

/**
 * Asks the API to fetch now rather than wait out its cache window, and returns the refreshed
 * table.
 *
 * <p>Rows the user entered themselves are untouched: this refreshes what was fetched, it does not
 * overwrite what was asserted. With `Features:AutomaticExchangeRates` off the call succeeds and
 * changes nothing, which is why the UI hides the control rather than relying on the response.</p>
 */
export async function refreshExchangeRates(): Promise<ExchangeRate[]> {
  const response = await api.post<ExchangeRate[]>('/ExchangeRates/refresh');
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
