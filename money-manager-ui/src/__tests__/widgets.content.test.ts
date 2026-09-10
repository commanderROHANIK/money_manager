/**
 * Asserts what a handful of widgets actually render, rather than only that they mount.
 *
 * The smoke suite next door proves every widget mounts warning-free, which catches missing
 * imports and bad props. It cannot catch a widget that mounts perfectly and shows the wrong
 * number — and for this product a wrong number is worse than a blank screen, because the whole
 * value proposition is that the figures can be trusted.
 *
 * Scope is deliberately small. These are the widgets where a rendering mistake would
 * misrepresent the portfolio, not a sample of the catalogue.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import type { PortfolioAnalytics, PropertyMetrics, RentSchedule } from '../models/models';
import { currentLocale, DEFAULT_LOCALE, intlLocale } from '../i18n/locale';
import { formatDate } from '../utils/labels';
import { formatMoney } from '../utils/money';
import * as f from './fixtures';
import { FROZEN_NOW } from './fixtures';

import PropertyMetricsWidget from '../components/Widgets/Properties/PropertyMetricsWidget.vue';
import PortfolioSummaryWidget from '../components/Widgets/Properties/PortfolioSummaryWidget.vue';
import RentCollectionWidget from '../components/Widgets/Properties/RentCollectionWidget.vue';
import TransactionLedgerWidget from '../components/Widgets/Properties/TransactionLedgerWidget.vue';
import UnderpricedPropertiesWidget from '../components/Widgets/Properties/UnderpricedPropertiesWidget.vue';
import TotalRentWidget from '../components/Widgets/Properties/TotalRentWidget.vue';

// These assert what a widget *shows* — the figures and the labels — not how a locale formats
// them. Pinned to English so the expectations below stay readable and stable; the formatting
// itself is covered in both languages by the colocated tests on formatMoney, formatPercent and
// formatDate, which is where a locale bug would actually live.
beforeEach(() => {
  currentLocale.value = 'en';
});

afterEach(() => {
  currentLocale.value = DEFAULT_LOCALE;
});

beforeEach(() => {
  // Date only — faking timers wholesale would stall the async settling these widgets do.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(FROZEN_NOW);
});

afterEach(() => vi.useRealTimers());

describe('PropertyMetricsWidget', () => {
  it('renders an unknown metric as a dash, never as zero', () => {
    // The product's central promise. A spreadsheet gives you a confident wrong number; this
    // says which inputs are missing. Rendering null as 0% would assert something false about
    // the property — that it returned nothing, rather than that we cannot tell.
    const wrapper = mount(PropertyMetricsWidget, {
      props: { metrics: f.propertyMetricsUnknown as unknown as PropertyMetrics },
    });

    const text = wrapper.text();

    expect(text).toContain('—');
    expect(text).not.toContain('0.0%');
    expect(text).not.toContain('0.00%');
  });

  it('explains which inputs were missing', () => {
    const wrapper = mount(PropertyMetricsWidget, {
      props: { metrics: f.propertyMetricsUnknown as unknown as PropertyMetrics },
    });

    // The warnings are part of the answer, not an error state.
    expect(wrapper.text()).toContain('No valuation on record');
  });

  it('renders real figures when they are known', () => {
    const wrapper = mount(PropertyMetricsWidget, {
      props: { metrics: f.propertyMetrics as unknown as PropertyMetrics },
    });

    const text = wrapper.text();

    // formatPercent uses toFixed, which is locale-independent, so a literal is safe here.
    expect(text).toContain('22.3%'); // totalRoi 0.223

    // Money is not: formatMoney follows the *application's* locale, which these tests pin to
    // English. Computed rather than hardcoded for the original reason — the separator differs by
    // language — but computed against the same locale the widget uses, not the machine's, which
    // is what the two siblings above were already updated to and this one was missed.
    expect(text).toContain((43333).toLocaleString(intlLocale(), { maximumFractionDigits: 0 }));
  });
});

describe('PortfolioSummaryWidget', () => {
  it('refuses to total a portfolio spanning currencies with no rate, and names the pair', () => {
    // Adding HUF to EUR without a rate produces a plausible, unlabelled, wrong number. The API
    // returns nulls; the UI has to say which rate would fix it rather than invent a figure.
    //
    // This assertion used to require the tiles be hidden entirely, which is what the widget did
    // when no total could ever exist. Now that a rate can produce one, the tiles stay and show
    // the same em dash every other unknown figure in this product shows.
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolioMixedCurrency as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('HUF → EUR');
    expect(text).toContain('Cash invested—');
    expect(text).not.toContain('Cash invested0');
  });

  it('labels converted totals as converted, and shows the rate they came from', () => {
    // The caveat is not decoration. A converted total is derived from a number the user typed in
    // themselves, and has to read differently from a figure that came out of the ledger.
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolioConverted as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('Converted to EUR');
    expect(text).toContain('1 HUF = 0.0025 EUR');
    expect(text).toContain(formatDate('2026-07-01'));
    expect(text).not.toContain('No exchange rate on record');
  });

  it('says the rate came from the user when it did', () => {
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolioConverted as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('rate you entered');
    expect(text).not.toContain('ECB');
  });

  it('names the ECB when the rate was fetched rather than entered', () => {
    // The two fixtures differ in exactly one field, and produce the same totals. That is the
    // point: nothing about the figures themselves distinguishes a rate somebody asserted from one
    // the API looked up, so the sentence underneath is the only thing that can — and the line
    // this replaced claimed "the rates you entered" over both.
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolioConvertedFromEcb as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('ECB reference rate');
    expect(text).toContain('1 HUF = 0.0025 EUR');
    // The date shown is the one the applied rate carried, not today's.
    expect(text).toContain(formatDate('2026-08-10'));
    expect(text).not.toContain('rate you entered');
  });

  it('discloses the rate the total was built from, not the newest one on record', () => {
    // The acceptance criterion in one assertion: a stale applied rate is still what produced the
    // figures above it, so that is what gets disclosed. Looking the rate up again at render time
    // would show a number the total was never built from — a disclosure that is worse than none,
    // because it invites the reader to check the arithmetic and find it wrong.
    const stale = {
      ...f.portfolioConvertedFromEcb,
      appliedRates: [
        { from: 'HUF', to: 'EUR', rate: 0.0024, asOf: '2026-01-31T00:00:00', inverted: true, source: 1 },
      ],
    };

    const text = mount(PortfolioSummaryWidget, {
      props: { portfolio: stale as unknown as PortfolioAnalytics },
    }).text();

    expect(text).toContain('1 HUF = 0.0024 EUR');
    expect(text).toContain(formatDate('2026-01-31'));
  });

  it('says nothing about conversion when the portfolio never needed it', () => {
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolio as unknown as PortfolioAnalytics },
    });

    expect(wrapper.text()).not.toContain('Converted to');
  });

  it('totals a single-currency portfolio', () => {
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolio as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('Cash invested');
    expect(text).toContain('18.7%'); // portfolioRoi 0.187
    expect(text).not.toContain('several currencies');
  });

  it('invites the first property rather than rendering an empty grid', () => {
    const wrapper = mount(PortfolioSummaryWidget, {
      props: {
        portfolio: { ...f.portfolio, propertyCount: 0 } as unknown as PortfolioAnalytics,
      },
    });

    expect(wrapper.text()).toContain('No properties yet');
  });
});

describe('TransactionLedgerWidget', () => {
  it('shows direction from the category even though every amount is stored positive', () => {
    // The single sign convention: amounts are always positive and the category decides
    // direction. If a widget starts reading a sign off the amount instead, this fails.
    const wrapper = mount(TransactionLedgerWidget, { props: { transactions: f.transactions } });
    const rows = wrapper.text();

    // Rent received is income, insurance and repairs are not.
    expect(rows).toContain('+');
    expect(rows).toContain('−');
    expect(rows).toContain('Rent received');
    expect(rows).toContain('Insurance');
  });

  it('tells the user the entry convention', () => {
    const wrapper = mount(TransactionLedgerWidget, { props: { transactions: f.transactions } });

    expect(wrapper.text()).toContain('positive number');
  });

  it('says the ledger is what makes the figures real when it is empty', () => {
    const wrapper = mount(TransactionLedgerWidget, { props: { transactions: [] } });

    expect(wrapper.text()).toContain('No entries yet');
  });
});

describe('RentCollectionWidget', () => {
  const mountWith = (schedule: unknown) =>
    mount(RentCollectionWidget, {
      props: { schedule: schedule as RentSchedule, currencyCode: 'HUF' },
    });

  it('renders a vacant month as unknown rather than as nothing owed', () => {
    // The distinction the whole product turns on. A vacant month owed nothing; a let month that
    // collected nothing owed everything. Rendering both as 0 would make an empty property read
    // exactly like a fully collected one.
    const text = mountWith(f.rentScheduleWithVacancy).text();

    expect(text).toContain('Vacant');
    expect(text).toContain('—');
  });

  it('separates a month that is late from one that is merely unpaid', () => {
    // August is unpaid but not yet due, so it is not a debt and must not be counted as one.
    // May is unpaid and overdue. Both render as "Unpaid"; only one is in the arrears figure.
    const text = mountWith(f.rentSchedule).text();

    expect(text).toContain('Unpaid');
    expect(text).toContain('behind');

    // 280,000 = March's 40,000 shortfall + May's 240,000. August's 240,000 is excluded.
    expect(text).toContain((280000).toLocaleString(intlLocale(), { maximumFractionDigits: 0 }));
    expect(text).not.toContain((520000).toLocaleString(intlLocale(), { maximumFractionDigits: 0 }));
  });

  it('says it is up to date when nothing is overdue', () => {
    const text = mountWith({ ...f.rentSchedule, arrears: 0, overduePeriodCount: 0 }).text();

    expect(text).toContain('Up to date');
  });

  it('offers to record only the months that are actually owed', () => {
    // Not a settled month, and not a vacant one — pressing it there would either double-book the
    // rent or invent a charge nobody owes.
    const wrapper = mountWith(f.rentSchedule);
    const buttons = wrapper.findAll('button').filter((b) => b.text() === 'Mark received');

    // 2026-05 and 2026-08 are the two Unpaid rows.
    expect(buttons).toHaveLength(2);
  });

  it('asks the page to record the month it was pressed for', async () => {
    const wrapper = mountWith(f.rentSchedule);
    const button = wrapper.findAll('button').filter((b) => b.text() === 'Mark received')[0];

    await button.trigger('click');

    // Newest first, so the first button is August's.
    expect(wrapper.emitted('record')).toEqual([['2026-08']]);
  });

  it('shows the server’s reason when a record call was refused', () => {
    const wrapper = mount(RentCollectionWidget, {
      props: {
        schedule: f.rentSchedule as unknown as RentSchedule,
        currencyCode: 'HUF',
        error: 'Rent for 2026-05 is already recorded.',
      },
    });

    expect(wrapper.text()).toContain('already recorded');
  });

  it('invites a tenancy rather than rendering an empty table', () => {
    expect(mountWith(null).text()).toContain('No tenancy on record');
  });
});

describe('UnderpricedPropertiesWidget', () => {
  it('lists only properties charging below market', () => {
    // propertyMetricsAtMarket has a negative rent gap — it is charging above the estimate and
    // must not be presented as an opportunity to raise rent.
    const wrapper = mount(UnderpricedPropertiesWidget, {
      props: { portfolio: f.portfolio as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('Bartók flat');
    expect(text).not.toContain('Lakeside house');
  });

  it('shows the portfolio-converted total, not a client-side sum of the list', () => {
    const wrapper = mount(UnderpricedPropertiesWidget, {
      props: { portfolio: f.portfolio as unknown as PortfolioAnalytics },
    });

    // f.portfolio.totalAnnualRentUplift is the backend-computed figure — asserting on it directly
    // is what proves this widget renders the rollup rather than re-deriving its own from the list.
    expect(wrapper.text()).toContain(
      formatMoney(f.portfolio.totalAnnualRentUplift, f.portfolio.currency)
    );
  });
});

describe('TotalRentWidget', () => {
  it('shows the portfolio-converted total', () => {
    const wrapper = mount(TotalRentWidget, {
      props: { portfolio: f.portfolioConverted as unknown as PortfolioAnalytics },
    });

    expect(wrapper.text()).toContain(
      formatMoney(f.portfolioConverted.totalMonthlyRent, f.portfolioConverted.currency)
    );
  });

  it('shows a blank rather than a raw cross-currency sum when the rate is missing', () => {
    // The regression this guards: adding a HUF amount and a EUR amount as if the same unit and
    // labelling the result as one of them.
    const wrapper = mount(TotalRentWidget, {
      props: { portfolio: f.portfolioMixedCurrency as unknown as PortfolioAnalytics },
    });

    expect(wrapper.text()).toContain('—');
    expect(wrapper.text()).not.toMatch(/\d/);
  });
});
