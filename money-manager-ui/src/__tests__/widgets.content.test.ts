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
import type { PortfolioAnalytics, PropertyMetrics } from '../models/models';
import * as f from './fixtures';
import { FROZEN_NOW } from './fixtures';

import PropertyMetricsWidget from '../components/Widgets/Properties/PropertyMetricsWidget.vue';
import PortfolioSummaryWidget from '../components/Widgets/Properties/PortfolioSummaryWidget.vue';
import TransactionLedgerWidget from '../components/Widgets/Properties/TransactionLedgerWidget.vue';
import UnderpricedPropertiesWidget from '../components/Widgets/Properties/UnderpricedPropertiesWidget.vue';

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

    // Money is not: formatMoney follows the ambient locale, so this is '43,333' under en-US
    // and '43.333' under de-DE. Compute the expectation rather than hardcoding the separator.
    expect(text).toContain((43333).toLocaleString(undefined, { maximumFractionDigits: 0 }));
  });
});

describe('PortfolioSummaryWidget', () => {
  it('refuses to total a portfolio spanning currencies, and says why', () => {
    // Adding HUF to EUR without a rate produces a plausible, unlabelled, wrong number. The API
    // returns nulls and mixedCurrency; the UI has to explain rather than render a total.
    const wrapper = mount(PortfolioSummaryWidget, {
      props: { portfolio: f.portfolioMixedCurrency as unknown as PortfolioAnalytics },
    });

    const text = wrapper.text();

    expect(text).toContain('several currencies');
    expect(text).not.toContain('Cash invested');
    expect(text).not.toContain('Portfolio ROI');
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

describe('UnderpricedPropertiesWidget', () => {
  it('lists only properties charging below market', () => {
    // propertyMetricsAtMarket has a negative rent gap — it is charging above the estimate and
    // must not be presented as an opportunity to raise rent.
    const wrapper = mount(UnderpricedPropertiesWidget, {
      props: { metrics: f.portfolio.properties as unknown as PropertyMetrics[] },
    });

    const text = wrapper.text();

    expect(text).toContain('Bartók flat');
    expect(text).not.toContain('Lakeside house');
  });
});
