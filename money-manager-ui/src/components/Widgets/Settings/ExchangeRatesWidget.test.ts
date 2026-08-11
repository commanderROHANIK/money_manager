/**
 * The rate table is where a landlord decides whether to trust the converted totals everywhere
 * else, so what it has to get right is provenance: which of these numbers did I assert, and which
 * did the application go and find?
 *
 * Two rows that look identical apart from a badge is exactly the situation, which is why the
 * fixture holds one of each kind — and why the introduction has to change with the deployment
 * flag rather than describing fetching that may not be switched on.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import * as f from '../../../__tests__/fixtures';

const calls = vi.hoisted(() => ({ refresh: 0 }));

vi.mock('../../../services/exchangeRateApi', async () => {
  const fixtures = await import('../../../__tests__/fixtures');
  return {
    fetchExchangeRates: () => Promise.resolve(fixtures.exchangeRates),
    refreshExchangeRates: () => {
      calls.refresh += 1;
      return Promise.resolve(fixtures.exchangeRates);
    },
    upsertExchangeRate: () => Promise.resolve(fixtures.exchangeRates[0]),
    deleteExchangeRate: () => Promise.resolve(),
  };
});

import ExchangeRatesWidget from './ExchangeRatesWidget.vue';
import { setLocale } from '../../../i18n';
import { DEFAULT_LOCALE } from '../../../i18n/locale';
import { api } from '../../../services/api';
import { clearFeatures, ensureFeaturesLoaded } from '../../../services/features';

// English, so the expectations read as the sentences themselves. Which words each language uses
// is messages.test.ts's job; this file is about which sentence is chosen.
beforeEach(() => setLocale('en'));

afterEach(() => {
  setLocale(DEFAULT_LOCALE);
  clearFeatures();
  vi.restoreAllMocks();
});

beforeEach(() => {
  calls.refresh = 0;
  clearFeatures();
});

/** Loads the flags through the real store, with the server's answer stubbed at the transport. */
async function mountWith(automaticExchangeRates: boolean) {
  vi.spyOn(api, 'get').mockResolvedValue({
    data: { banking: false, stocks: false, loans: true, events: true, automaticExchangeRates },
  });

  await ensureFeaturesLoaded();

  const wrapper = mount(ExchangeRatesWidget);
  await settle();

  return wrapper;
}

async function settle() {
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

describe('ExchangeRatesWidget', () => {
  it('marks each row with where it came from', async () => {
    const wrapper = await mountWith(true);
    const text = wrapper.text();

    // The fixture holds one of each. A badge shown on neither, or on both alike, would leave the
    // table exactly as ambiguous as it was before rates were ever fetched.
    expect(text).toContain(`1 ${f.exchangeRates[0].baseCurrency} = ${f.exchangeRates[0].rate}`);
    expect(text).toContain('you entered this');
    expect(text).toContain('ECB reference rate');
  });

  it('describes fetching only where fetching happens', async () => {
    const on = (await mountWith(true)).text();

    expect(on).toContain('European Central Bank');
    // Reference rates, said out loud. Implying a tradeable figure is the specific dishonesty this
    // disclosure exists to avoid.
    expect(on).toContain('not tradeable rates');
    expect(on).not.toContain('nothing is fetched');

    clearFeatures();

    const off = (await mountWith(false)).text();

    expect(off).toContain('nothing is fetched');
    expect(off).not.toContain('European Central Bank');
  });

  it('offers a refresh only when the deployment fetches at all', async () => {
    const off = await mountWith(false);
    expect(off.text()).not.toContain('Refresh rates');

    clearFeatures();

    const on = await mountWith(true);
    const refresh = on.findAll('button').find((b) => b.text() === 'Refresh rates');

    expect(refresh).toBeDefined();

    await refresh!.trigger('click');
    await settle();

    // Asks the server to fetch now rather than reloading the same cached table, which is the
    // difference between the button doing something and appearing to.
    expect(calls.refresh).toBe(1);
    expect(on.emitted('changed')).toHaveLength(1);
  });

  it('renders the fetching copy in Hungarian too', async () => {
    // The customer this is being built for reads Hungarian, and every string here is new. A key
    // that exists only in en.json renders as `settings.ratesProvider` on their screen, which is
    // the one failure the four locale files exist to prevent.
    setLocale('hu');

    const text = (await mountWith(true)).text();

    expect(text).toContain('Európai Központi Bank');
    expect(text).toContain('EKB-referenciaárfolyam');
    expect(text).toContain('Árfolyamok frissítése');
    expect(text).not.toMatch(/settings\./);
    expect(text).not.toContain('{');
  });
});
