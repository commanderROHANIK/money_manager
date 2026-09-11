/**
 * The smoke suite mounts this once with fixture props and never exercises a handler. Two things
 * worth a dedicated test: money renders in each holding's own currency rather than a hardcoded
 * HUF/hu-HU formatter, and deleting refetches the list rather than leaving a stale row on a
 * failed or successful delete.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import { setLocale } from '../../../i18n';
import { DEFAULT_LOCALE } from '../../../i18n/locale';
import HoldingsListWidget from './HoldingsListWidget.vue';
import { stocks } from '../../../__tests__/fixtures';

const fetchStocks = vi.fn();
const deleteStock = vi.fn();

vi.mock('../../../services/api', () => ({
  fetchStocks: () => fetchStocks(),
  deleteStock: (id: number) => deleteStock(id),
}));

beforeEach(() => {
  setLocale('en');
  fetchStocks.mockReset().mockResolvedValue(stocks);
  deleteStock.mockReset().mockResolvedValue(undefined);
});

afterEach(() => setLocale(DEFAULT_LOCALE));

describe('HoldingsListWidget', () => {
  it("formats each holding's value in its own currency rather than a hardcoded one", async () => {
    fetchStocks.mockResolvedValue([
      { id: 9, ticker: 'AAPL', sharesOwned: 2, purchasePrice: 100, currentPrice: 120, purchaseDate: '2026-01-01', currencyCode: 'USD' },
    ]);

    const wrapper = mount(HoldingsListWidget);
    await flushPromises();

    expect(wrapper.text()).toContain('$');
    expect(wrapper.text()).not.toContain('Ft');
  });

  it('gives the delete control an accessible name naming the ticker', async () => {
    const wrapper = mount(HoldingsListWidget);
    await flushPromises();

    expect(wrapper.find(`[aria-label="Remove ${stocks[0].ticker}"]`).exists()).toBe(true);
  });

  it('deletes a holding and refetches the list', async () => {
    const wrapper = mount(HoldingsListWidget);
    await flushPromises();

    fetchStocks.mockResolvedValue(stocks.slice(1));

    await wrapper.find(`[aria-label="Remove ${stocks[0].ticker}"]`).trigger('click');
    await flushPromises();

    expect(deleteStock).toHaveBeenCalledWith(stocks[0].id);
    expect(fetchStocks).toHaveBeenCalledTimes(2);
    expect(wrapper.text()).not.toContain(stocks[0].ticker);
  });

  it('does not crash the widget when a delete fails', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    deleteStock.mockRejectedValue(new Error('conflict'));

    const wrapper = mount(HoldingsListWidget);
    await flushPromises();

    await wrapper.find(`[aria-label="Remove ${stocks[0].ticker}"]`).trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain(stocks[0].ticker);
  });
});
