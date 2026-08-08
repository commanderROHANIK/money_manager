/**
 * The two widgets that consume the bank-balance rollup.
 *
 * Both used to format every figure as HUF with a hardcoded `hu-HU` formatter, on top of an
 * endpoint that added balances across currencies without looking at `CurrencyCode`. Between them
 * that turned a EUR account plus a HUF account into a single confident number in the wrong unit —
 * the exact failure this product's whole design is arranged to prevent. These tests pin the
 * replacement behaviour so it cannot quietly come back.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import type { Component } from 'vue';
import * as f from './fixtures';

const state = vi.hoisted(() => ({
  summary: null as unknown,
  stocks: [] as unknown[],
}));

vi.mock('../services/api', () => ({
  fetchBankAccountsTotalBalance: () => Promise.resolve(state.summary),
  fetchStocks: () => Promise.resolve(state.stocks),
}));

import TotalBalance from '../components/Widgets/BankAccounts/TotalBalance.vue';
import CashVsInvestedWidget from '../components/Widgets/Stocks/CashVsInvestedWidget.vue';

async function render(component: Component) {
  const wrapper = mount(component);
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
  return wrapper.text();
}

beforeEach(() => {
  state.summary = f.bankBalanceSummary;
  state.stocks = [];
});

describe('TotalBalance', () => {
  it('renders the balance in the currency the server reports, not a hardcoded one', async () => {
    const text = await render(TotalBalance);

    // 2,358,350 HUF. Formatted as EUR it would read as a figure 400 times too large.
    expect(text).toContain('2,358,350');
    expect(text).not.toContain('€');
  });

  it('shows an unknown total as a dash and names the rate that would fix it', async () => {
    state.summary = f.bankBalanceSummaryUnconvertible;

    const text = await render(TotalBalance);

    expect(text).toContain('—');
    expect(text).toContain('HUF→EUR');
    // The dangerous alternative: 2,359,550 of nothing in particular.
    expect(text).not.toContain('2,359,550');
  });

  it('says when the figure came from a conversion', async () => {
    state.summary = { ...f.bankBalanceSummary, converted: true, currency: 'EUR', totalBalance: 5895 };

    expect(await render(TotalBalance)).toContain('Converted to EUR');
  });
});

describe('CashVsInvestedWidget', () => {
  it('adds cash to holdings when they share a currency', async () => {
    state.stocks = [{ sharesOwned: 2, currentPrice: 1000, currencyCode: 'HUF' }];

    const text = await render(CashVsInvestedWidget);

    expect(text).toContain('2,360,350'); // 2,358,350 + 2,000
  });

  it('refuses to add cash to holdings in another currency, and says why', async () => {
    state.stocks = [{ sharesOwned: 2, currentPrice: 1000, currencyCode: 'EUR' }];

    const text = await render(CashVsInvestedWidget);

    expect(text).toContain('not added together');
    expect(text).not.toContain('2,360,350');
  });

  it('reports an unknown cash total as unknown rather than as zero', async () => {
    state.summary = f.bankBalanceSummaryUnconvertible;
    state.stocks = [{ sharesOwned: 2, currentPrice: 1000, currencyCode: 'EUR' }];

    const text = await render(CashVsInvestedWidget);

    // Treating a missing figure as 0 would report the holdings alone as the whole net position.
    expect(text).toContain('exchange rate');
    expect(text).toContain('—');
  });
});
