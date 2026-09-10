/**
 * The widgets that consume the bank-balance and stock-value rollups.
 *
 * The bank-balance pair used to format every figure as HUF with a hardcoded `hu-HU` formatter, on
 * top of an endpoint that added balances across currencies without looking at `CurrencyCode`.
 * DividendIncomeWidget had the same defect on the stocks side, and worse: it never even said the
 * total was suspect, unlike CashVsInvestedWidget's own (blank-out) handling of the same case.
 * Between them that turned a EUR account plus a HUF account, or a EUR holding plus a HUF holding,
 * into a single confident number in the wrong unit — the exact failure this product's whole
 * design is arranged to prevent. These tests pin the replacement behaviour so it cannot quietly
 * come back.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import type { Component } from 'vue';
import { currentLocale, DEFAULT_LOCALE } from '../i18n/locale';
import * as f from './fixtures';

const state = vi.hoisted(() => ({
  summary: null as unknown,
  invested: null as unknown,
}));

vi.mock('../services/api', () => ({
  fetchBankAccountsTotalBalance: () => Promise.resolve(state.summary),
  fetchStocksTotalValue: () => Promise.resolve(state.invested),
}));

/** A StockValueSummary shaped like the one CurrencyRollup.Sum actually produces. */
function stockSummary(totalValue: number | null, currency: string) {
  return {
    totalValue,
    currency,
    mixedCurrency: false,
    converted: false,
    baseCurrency: currency,
    byCurrency: totalValue === null ? [] : [{ currencyCode: currency, total: totalValue }],
    missingRates: [],
    appliedRates: [],
    warnings: [],
  };
}

import TotalBalance from '../components/Widgets/BankAccounts/TotalBalance.vue';
import CashVsInvestedWidget from '../components/Widgets/Stocks/CashVsInvestedWidget.vue';
import DividendIncomeWidget from '../components/Widgets/Stocks/DividendIncomeWidget.vue';

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

async function render(component: Component) {
  const wrapper = mount(component);
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
  return wrapper.text();
}

beforeEach(() => {
  state.summary = f.bankBalanceSummary;
  state.invested = stockSummary(0, 'HUF');
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
    state.invested = stockSummary(2000, 'HUF'); // 2 shares @ 1,000

    const text = await render(CashVsInvestedWidget);

    expect(text).toContain('2,360,350'); // 2,358,350 + 2,000
  });

  it('refuses to add cash to holdings in another currency, and says why', async () => {
    state.invested = stockSummary(2000, 'EUR');

    const text = await render(CashVsInvestedWidget);

    expect(text).toContain('not added together');
    expect(text).not.toContain('2,360,350');
  });

  it('reports an unknown cash total as unknown rather than as zero', async () => {
    state.summary = f.bankBalanceSummaryUnconvertible;
    state.invested = stockSummary(2000, 'EUR');

    const text = await render(CashVsInvestedWidget);

    // Treating a missing figure as 0 would report the holdings alone as the whole net position.
    expect(text).toContain('exchange rate');
    expect(text).toContain('—');
  });

  it('shows a real converted total instead of blanking out when holdings are mixed but a rate exists', async () => {
    // The regression this guards: mixed-currency holdings used to always render blank, even when
    // a rate existed to convert them — the same "confident wrong number, or nothing at all"
    // false choice this rollup work exists to end.
    state.invested = { ...stockSummary(3200, 'EUR'), mixedCurrency: true, converted: true };

    const text = await render(CashVsInvestedWidget);

    // The converted figure itself renders, not a blank — cash and holdings still don't share a
    // currency (HUF vs. EUR), so they're reported separately rather than combined.
    expect(text).toContain('3,200');
    expect(text).toContain('not added together');
  });
});

describe('DividendIncomeWidget', () => {
  it('estimates from the converted holdings total', async () => {
    state.invested = stockSummary(100000, 'HUF');

    const text = await render(DividendIncomeWidget);

    expect(text).toContain('2,000'); // 100,000 * 2% assumed yield
  });

  it('shows a blank rather than a raw cross-currency sum when the rate is missing', async () => {
    // The regression this guards: this widget used to add a EUR holding and a HUF holding as if
    // the same unit, and — unlike every other total in the app — never said so. It just showed a
    // wrong number as if it meant something.
    state.invested = stockSummary(null, 'EUR');

    const text = await render(DividendIncomeWidget);

    expect(text).toContain('—');
    // No formatted money figure anywhere (the static "2% yield" caption does contain a digit,
    // which is fine — a comma-grouped number is what a real, wrong total would look like).
    expect(text).not.toMatch(/\d{1,3}(,\d{3})+/);
  });
});
