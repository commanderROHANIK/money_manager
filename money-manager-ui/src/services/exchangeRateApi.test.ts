/**
 * Pins the method, URL and body of every call in the exchange-rate and settings services.
 *
 * The URLs are a contract with the API's route templates. A wrong one produces a 404 that
 * surfaces as an empty rate list rather than an error — and an empty rate list is precisely what
 * a portfolio with no rates looks like, so the failure would be invisible.
 *
 * Requests go through a swapped axios adapter so the real interceptor chain still runs.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { InternalAxiosRequestConfig } from 'axios';
import { api } from './api';
import * as rates from './exchangeRateApi';
import * as settings from './settingsApi';

const originalAdapter = api.defaults.adapter;
let seen: InternalAxiosRequestConfig;

beforeEach(() => {
  api.defaults.adapter = async (config) => {
    seen = config as InternalAxiosRequestConfig;
    return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
  };
});

afterEach(() => {
  api.defaults.adapter = originalAdapter;
  vi.restoreAllMocks();
});

const CALLS: [string, string, string, () => Promise<unknown>][] = [
  ['fetchExchangeRates', 'get', '/ExchangeRates',
    () => rates.fetchExchangeRates()],
  // POST rather than GET, because it has an effect: it spends an outbound request and writes
  // rows. A refresh reachable by GET is one a browser or a link prefetcher can trigger on its own.
  ['refreshExchangeRates', 'post', '/ExchangeRates/refresh',
    () => rates.refreshExchangeRates()],
  ['upsertExchangeRate', 'put', '/ExchangeRates/EUR/HUF',
    () => rates.upsertExchangeRate('EUR', 'HUF', 400)],
  ['deleteExchangeRate', 'delete', '/ExchangeRates/EUR/HUF',
    () => rates.deleteExchangeRate('EUR', 'HUF')],
];

const PINNED_NAMES = CALLS.map(([name]) => name);

describe('exchange rate request shapes', () => {
  it.each(CALLS)('%s issues %s %s', async (_name, method, url, call) => {
    await call();

    expect(seen.method).toBe(method);
    expect(seen.url).toBe(url);
  });

  it('covers every exported function', () => {
    const exported = Object.entries(rates)
      .filter(([, v]) => typeof v === 'function')
      .map(([name]) => name)
      .sort();

    expect(exported).toEqual([...PINNED_NAMES].sort());
  });

  it('sends the rate and the date it was recorded', async () => {
    await rates.upsertExchangeRate('EUR', 'HUF', 400.5, '2026-07-01');

    expect(JSON.parse(seen.data)).toEqual({ rate: 400.5, asOf: '2026-07-01' });
  });

  it('puts the pair in the path, in the direction it was entered', async () => {
    // Direction is the whole meaning of the row: HUF/EUR and EUR/HUF differ by a factor of
    // 160,000, and both look like plausible money.
    await rates.upsertExchangeRate('HUF', 'EUR', 0.0025);

    expect(seen.url).toBe('/ExchangeRates/HUF/EUR');
  });
});

describe('settings request shapes', () => {
  it('reads settings from /Settings', async () => {
    await settings.fetchSettings();

    expect(seen.method).toBe('get');
    expect(seen.url).toBe('/Settings');
  });

  it('sends both preferences on update', async () => {
    await settings.updateSettings({ baseCurrency: 'HUF', alwaysConvertToBaseCurrency: true });

    expect(seen.method).toBe('put');
    expect(seen.url).toBe('/Settings');
    expect(JSON.parse(seen.data)).toEqual({ baseCurrency: 'HUF', alwaysConvertToBaseCurrency: true });
  });
});
