/**
 * Pins the method and URL of every call in the property service.
 *
 * These are thin wrappers, so this is regression protection rather than logic testing — but the
 * URLs are a contract with the API's nested controller routes
 * (`api/RentalProperties/{propertyId}/leases` and friends), and getting one wrong produces a 404
 * that surfaces as an empty widget rather than an error. That is exactly the kind of defect this
 * codebase is set up to avoid: silent, plausible, and wrong.
 *
 * Requests go through a swapped axios adapter so the real interceptor chain still runs.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import type { InternalAxiosRequestConfig } from 'axios';
import { api } from './api';
import * as p from './propertyApi';

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

const PROPERTY_ID = 7;

// [name, method, url, call] — the name and the two strings feed the %s placeholders in the
// title, so a failure reads "fetchRentHistory issues get /RentalProperties/7/rent-history"
// rather than printing the closure source.
const CALLS: [string, string, string, () => Promise<unknown>][] = [
    ['createProperty', 'post', '/RentalProperties',
      () => p.createProperty({} as p.RentalPropertyRequest)],
    ['updateProperty', 'put', '/RentalProperties/7',
      () => p.updateProperty(PROPERTY_ID, {} as p.RentalPropertyRequest)],
    ['fetchProperty', 'get', '/RentalProperties/7',
      () => p.fetchProperty(PROPERTY_ID)],
    ['fetchPropertyMetrics', 'get', '/RentalProperties/7/analytics',
      () => p.fetchPropertyMetrics(PROPERTY_ID)],
    ['fetchPortfolioAnalytics', 'get', '/RentalProperties/analytics/portfolio',
      () => p.fetchPortfolioAnalytics()],
    ['fetchTransactions', 'get', '/RentalProperties/7/transactions',
      () => p.fetchTransactions(PROPERTY_ID)],
    ['createTransaction', 'post', '/RentalProperties/7/transactions',
      () => p.createTransaction(PROPERTY_ID, {} as p.TransactionRequest)],
    ['deleteTransaction', 'delete', '/RentalProperties/7/transactions/3',
      () => p.deleteTransaction(PROPERTY_ID, 3)],
    ['fetchLeases', 'get', '/RentalProperties/7/leases',
      () => p.fetchLeases(PROPERTY_ID)],
    ['createLease', 'post', '/RentalProperties/7/leases',
      () => p.createLease(PROPERTY_ID, {} as p.LeaseRequest)],
    ['fetchRentHistory', 'get', '/RentalProperties/7/rent-history',
      () => p.fetchRentHistory(PROPERTY_ID)],
    ['addMarketEstimate', 'post', '/RentalProperties/7/rent-history/market-estimate',
      () => p.addMarketEstimate(PROPERTY_ID, 1000)],
    ['fetchValuations', 'get', '/RentalProperties/7/valuations',
      () => p.fetchValuations(PROPERTY_ID)],
    ['createValuation', 'post', '/RentalProperties/7/valuations',
      () => p.createValuation(PROPERTY_ID, '2026-01-01', 100)],
    ['fetchPropertyEvents', 'get', '/RentalProperties/7/events',
      () => p.fetchPropertyEvents(PROPERTY_ID)],
];

/** Drives the completeness check below, from the same table the assertions use. */
const PINNED_NAMES = CALLS.map(([name]) => name);

describe('request shapes', () => {
  it.each(CALLS)('%s issues %s %s', async (_name, method, url, call) => {
    await call();

    expect(seen.method).toBe(method);
    expect(seen.url).toBe(url);
  });

  it('covers every exported function', () => {
    // Compares names, not a count. A count passes when one function is added and another
    // removed in the same commit, and it never checks the table it claims to guard — so it
    // would have missed exactly the drift it exists to catch.
    const exported = Object.entries(p)
      .filter(([, v]) => typeof v === 'function')
      .map(([name]) => name)
      .sort();

    expect(exported).toEqual([...PINNED_NAMES].sort());
  });
});

describe('request bodies', () => {
  it('sends the market estimate amount and effective date', async () => {
    await p.addMarketEstimate(PROPERTY_ID, 285000, '2026-01-01');

    expect(JSON.parse(seen.data)).toEqual({ amount: 285000, effectiveFrom: '2026-01-01' });
  });

  it('defaults a valuation to the owner-estimate source', async () => {
    // `source = 1` is ValuationSource.OwnerEstimate. Defaulting to 0 would silently record
    // manual valuations as PurchasePrice, which the analytics treat differently.
    await p.createValuation(PROPERTY_ID, '2026-02-01', 74_000_000);

    expect(JSON.parse(seen.data)).toEqual({
      valuedOn: '2026-02-01',
      value: 74_000_000,
      source: 1,
    });
  });

  it('lets the caller override the valuation source', async () => {
    await p.createValuation(PROPERTY_ID, '2026-02-01', 74_000_000, 2);

    expect(JSON.parse(seen.data).source).toBe(2);
  });
});
