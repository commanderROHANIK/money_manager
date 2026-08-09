/**
 * Mounts every widget once with representative data and fails on any Vue warning.
 *
 * This exists because `vue-tsc` and `vite build` both pass on a template that references a
 * component the script block never imported — Vue only complains at runtime, by rendering the
 * tag as an unknown element. TenancyWidget shipped exactly that way: its form referenced
 * BaseInput, BaseButton and ListRow with no imports, so the whole tenancy form silently
 * rendered as nothing usable, through a green build.
 *
 * Treating warnings as failures also catches missing required props and bad v-model targets.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import type { Component } from 'vue';
import * as f from './fixtures';
import { FROZEN_NOW } from './fixtures';

// Chart.js needs a real canvas; jsdom has none. The chart wrappers are not what this test is
// about, so stand them down to placeholders.
vi.mock('vue-chartjs', async () => {
  // Imports go inside the factory: vi.mock is hoisted above the file's own imports.
  const { defineComponent, h } = await import('vue');
  const stub = (name: string) => defineComponent({ name, render: () => h('canvas') });
  return { Bar: stub('Bar'), Line: stub('Line'), Pie: stub('Pie'), Doughnut: stub('Doughnut') };
});

vi.mock('../services/api', async () => {
  const f = await import('./fixtures');
  return {
  TOKEN_STORAGE_KEY: 'token',
  api: {},
  fetchUpcomingEvents: () => Promise.resolve(f.upcomingEvents),
  updateUpcomingEvent: () => Promise.resolve(),
  deleteUpcomingEvent: () => Promise.resolve(),
  createUpcomingEvent: () => Promise.resolve(f.upcomingEvents[0]),
  fetchBankAccounts: () => Promise.resolve(f.bankAccounts),
  fetchBankAccountsTotalBalance: () => Promise.resolve(f.bankBalanceSummary),
  createBankAccount: () => Promise.resolve(f.bankAccounts[0]),
  updateBankAccount: () => Promise.resolve(),
  deleteBankAccount: () => Promise.resolve(),
  fetchLoans: () => Promise.resolve(f.loans),
  createLoan: () => Promise.resolve(f.loans[0]),
  updateLoan: () => Promise.resolve(),
  deleteLoan: () => Promise.resolve(),
  fetchRentalProperties: () => Promise.resolve(f.properties),
  createRentalProperty: () => Promise.resolve(f.properties[0]),
  updateRentalProperty: () => Promise.resolve(),
  deleteRentalProperty: () => Promise.resolve(),
  fetchStocks: () => Promise.resolve(f.stocks),
  createStock: () => Promise.resolve(f.stocks[0]),
  updateStock: () => Promise.resolve(),
  deleteStock: () => Promise.resolve(),
  };
});

vi.mock('../services/propertyApi', async () => {
  const f = await import('./fixtures');
  return {
  createProperty: () => Promise.resolve(f.properties[0]),
  updateProperty: () => Promise.resolve(),
  fetchProperty: () => Promise.resolve(f.properties[0]),
  fetchPropertyMetrics: () => Promise.resolve(f.propertyMetrics),
  fetchPortfolioAnalytics: () => Promise.resolve(f.portfolio),
  fetchTransactions: () => Promise.resolve(f.transactions),
  createTransaction: () => Promise.resolve(f.transactions[0]),
  deleteTransaction: () => Promise.resolve(),
  fetchLeases: () => Promise.resolve(f.leases),
  createLease: () => Promise.resolve(f.leases[0]),
  fetchRentHistory: () => Promise.resolve(f.rentHistory),
  addMarketEstimate: () => Promise.resolve(f.rentHistory[0]),
  fetchValuations: () => Promise.resolve(f.valuations),
  createValuation: () => Promise.resolve(f.valuations[0]),
  fetchPropertyEvents: () => Promise.resolve(f.propertyEvents),
  fetchRentSchedule: () => Promise.resolve(f.rentSchedule),
  recordRentForPeriod: () => Promise.resolve(f.rentSchedule.periods[0]),
  fetchArrears: () => Promise.resolve(f.arrears),
  };
});

// Mocked here rather than in fixtures.ts: vi.mock is file-scoped and hoisted, so a widget whose
// service is not stubbed in this file fires a real axios request into jsdom.
vi.mock('../services/exchangeRateApi', async () => {
  const f = await import('./fixtures');
  return {
    fetchExchangeRates: () => Promise.resolve(f.exchangeRates),
    upsertExchangeRate: () => Promise.resolve(f.exchangeRates[0]),
    deleteExchangeRate: () => Promise.resolve(),
  };
});

vi.mock('../services/settingsApi', async () => {
  const f = await import('./fixtures');
  return {
    fetchSettings: () => Promise.resolve(f.settings),
    updateSettings: () => Promise.resolve(f.settings),
  };
});

const widgets = import.meta.glob('../components/Widgets/**/*.vue', { eager: true }) as Record<
  string,
  { default: Component }
>;

// Props for widgets that take them; the rest load through the mocked services above.
const props: Record<string, Record<string, unknown>> = {
  BankAccountPieChart: { accounts: f.bankAccounts },
  PastEventsWidget: { events: f.upcomingEvents },
  LoanListWidget: { loans: f.loans },
  LoanStatusPieWidget: { loans: f.loans },
  MonthlyRepaymentChartWidget: { accounts: f.loans },
  NextDueRepaymentWidget: { loans: f.loans },
  TopLoansWidget: { loans: f.loans },
  TotalLoanAmountWidget: { loans: f.loans },
  MostExpensivePropertyWidget: { properties: f.properties },
  PortfolioSummaryWidget: { portfolio: f.portfolio },
  PropertyListWidget: { properties: f.properties, arrears: f.arrears },
  PropertyMetricsWidget: { metrics: f.propertyMetrics },
  PropertyTimelineWidget: { events: f.propertyEvents },
  RentByMonthChartWidget: { properties: f.properties },
  RentCollectionWidget: { schedule: f.rentSchedule, currencyCode: 'HUF' },
  RentOverTimeChartWidget: { history: f.rentHistory, currencyCode: 'HUF' },
  RentVsMarketWidget: { metrics: f.propertyMetrics },
  RentedVsVacantPieWidget: { properties: f.properties },
  TenancyWidget: { leases: f.leases },
  TotalRentWidget: { properties: f.properties },
  TransactionLedgerWidget: { transactions: f.transactions },
  UnderpricedPropertiesWidget: { metrics: f.portfolio.properties },
  UpcomingRentDueWidget: { properties: f.properties },
  ValuationWidget: { valuations: f.valuations, currencyCode: 'HUF' },
};

const name = (path: string) => path.split('/').pop()!.replace('.vue', '');

let warnings: string[] = [];
let warnSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  // The fixtures carry fixed 2026 dates while several widgets compare against `new Date()`, so
  // without a frozen clock this suite would quietly change behaviour over time and eventually
  // fail on a date nobody chose.
  //
  // Only Date is faked. Faking setTimeout as well would stop the `await setTimeout(0)` below
  // from ever resolving, and every service-backed widget would hang until the test timed out.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(FROZEN_NOW);

  warnings = [];
  warnSpy = vi.spyOn(console, 'warn').mockImplementation((...args: unknown[]) => {
    warnings.push(args.map(String).join(' '));
  });
});

afterEach(() => {
  warnSpy.mockRestore();
  vi.useRealTimers();
});

/**
 * Values that mean a template interpolated something it should have formatted. Cheap to check,
 * and it applies to every widget at once — which is what makes it worth doing generically
 * rather than widget by widget.
 */
const PLACEHOLDER_LEAKS = ['undefined', 'NaN', '[object Object]', 'Infinity'];

describe('widget smoke test', () => {
  const entries = Object.entries(widgets);

  it('covers every widget in the tree', () => {
    expect(entries.length).toBeGreaterThan(35);
  });

  it.each(entries.map(([path, mod]) => [name(path), mod.default] as const))(
    '%s mounts without warnings',
    async (widgetName, component) => {
      const wrapper = mount(component, {
        props: props[widgetName] ?? {},
        // Widgets link to detail pages; the router itself is not under test here.
        global: { stubs: { RouterLink: { props: ['to'], template: '<a><slot /></a>' } } },
      });
      // Let the onMounted fetches in the service-backed widgets settle.
      await new Promise((resolve) => setTimeout(resolve, 0));
      await nextTick();

      // "Failed to resolve component" is the one this suite exists for.
      expect(warnings.join('\n')).toBe('');
      expect(wrapper.html()).not.toBe('');

      // `expect(html).not.toBe('')` passes on `<div></div>`. A widget that renders no text and
      // no chart is not actually rendering.
      const text = wrapper.text().trim();
      expect(
        text !== '' || wrapper.find('canvas').exists(),
        `${widgetName} rendered neither text nor a chart`
      ).toBe(true);

      // A field added to a model but formatted wrong shows up here rather than in production.
      for (const leak of PLACEHOLDER_LEAKS) {
        expect(text, `${widgetName} leaked "${leak}" into its output`).not.toContain(leak);
      }

      wrapper.unmount();
    }
  );
});
