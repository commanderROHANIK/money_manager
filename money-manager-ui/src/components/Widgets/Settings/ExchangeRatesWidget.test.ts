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

const calls = vi.hoisted(() => ({
  refresh: 0,
  deleted: [] as string[],
  upserted: 0,
}));

// The list the widget reloads from. Mutable so a test can say what the server would answer once
// a manual row has been handed back to the provider.
const served = vi.hoisted(() => ({ rates: [] as unknown[] }));

vi.mock('../../../services/exchangeRateApi', async () => {
  const fixtures = await import('../../../__tests__/fixtures');
  served.rates = [...fixtures.exchangeRates];

  return {
    fetchExchangeRates: () => Promise.resolve(served.rates),
    refreshExchangeRates: () => {
      calls.refresh += 1;
      return Promise.resolve(served.rates);
    },
    upsertExchangeRate: () => {
      calls.upserted += 1;
      return Promise.resolve(fixtures.exchangeRates[0]);
    },
    deleteExchangeRate: (base: string, quote: string) => {
      calls.deleted.push(`${base}>${quote}`);
      return Promise.resolve();
    },
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
  calls.deleted = [];
  calls.upserted = 0;
  served.rates = [...f.exchangeRates];
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

  it('offers a way to hand a hand-entered pair back to the provider', async () => {
    // The gap this closes. Fetching only ever filled in pairs nobody had typed a rate for, which
    // is correct and completely invisible: someone who had entered EUR/HUF once had no way to say
    // "use whatever today's is" short of guessing that deleting the row would do it. Reported as
    // "it still doesn't work unless I record the rate by hand".
    const wrapper = await mountWith(true);

    const manual = f.exchangeRates[0];
    const button = wrapper.findAll('button').find((b) => b.text() === 'Use live rate');

    expect(button).toBeDefined();

    // Once it is gone the server has nothing hand-entered for the pair, so the next read fetches.
    served.rates = f.exchangeRates.filter((r) => r.id !== manual.id);

    await button!.trigger('click');
    await settle();

    expect(calls.deleted).toEqual([`${manual.baseCurrency}>${manual.quoteCurrency}`]);
    expect(wrapper.emitted('changed')).toHaveLength(1);
  });

  it('offers it only on the rows the user typed in', async () => {
    const wrapper = await mountWith(true);

    // The ECB row is already live; a control saying "use the live rate" on it would be a button
    // that does nothing, and one that deleted the row would be actively wrong.
    const buttons = wrapper.findAll('button').filter((b) => b.text() === 'Use live rate');

    expect(buttons).toHaveLength(f.exchangeRates.filter((r) => r.source === 0).length);
  });

  it('says so when nothing came back, rather than leaving a quietly emptier table', async () => {
    const wrapper = await mountWith(true);

    // The pair is deleted and the provider has nothing for it — an unreachable provider, or a
    // currency the ECB does not publish. The user's own figure is gone by then, which is what
    // they asked for, and precisely why it has to be said out loud.
    served.rates = [];

    await wrapper.findAll('button').find((b) => b.text() === 'Use live rate')!.trigger('click');
    await settle();

    expect(wrapper.text()).toContain('No rate has arrived for that pair yet');
  });

  it('takes no amount when the checkbox is ticked', async () => {
    const wrapper = await mountWith(true);

    const checkbox = wrapper.find('input[type="checkbox"]');
    expect(checkbox.exists()).toBe(true);

    await checkbox.setValue(true);
    await settle();

    // Typing a number is the thing being opted out of, so the field goes rather than sitting
    // there greyed out inviting a value that would be discarded.
    expect(wrapper.find('input[type="number"]').exists()).toBe(false);

    served.rates = [];
    await wrapper.find('form').trigger('submit');
    await settle();

    // Saved as "this pair is nobody's opinion", not as a rate of zero.
    expect(calls.upserted).toBe(0);
    expect(calls.deleted).toHaveLength(1);
  });

  it('hides the checkbox where fetching is switched off', async () => {
    const wrapper = await mountWith(false);

    // Offering it would promise a live rate the deployment has no way to get.
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false);
    expect(wrapper.findAll('button').some((b) => b.text() === 'Use live rate')).toBe(false);
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
