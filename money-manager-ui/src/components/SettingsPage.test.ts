/**
 * The settings page owns the two controls that decide what unit every consolidated total is
 * reported in, so the thing worth pinning is that saving one refreshes the other: changing the
 * base currency changes which pairs matter, and a rate list left showing the old answer is how a
 * user ends up entering a rate they do not need and missing the one they do.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import * as f from '../__tests__/fixtures';

const calls = vi.hoisted(() => ({ fetchRates: 0, refreshRates: 0, updateSettings: 0 }));

vi.mock('../services/exchangeRateApi', async () => {
  const fixtures = await import('../__tests__/fixtures');
  return {
    fetchExchangeRates: () => {
      calls.fetchRates += 1;
      return Promise.resolve(fixtures.exchangeRates);
    },
    refreshExchangeRates: () => {
      calls.refreshRates += 1;
      return Promise.resolve(fixtures.exchangeRates);
    },
    upsertExchangeRate: () => Promise.resolve(fixtures.exchangeRates[0]),
    deleteExchangeRate: () => Promise.resolve(),
  };
});

vi.mock('../services/settingsApi', async () => {
  const fixtures = await import('../__tests__/fixtures');
  return {
    fetchSettings: () => Promise.resolve(fixtures.settings),
    updateSettings: () => {
      calls.updateSettings += 1;
      return Promise.resolve(fixtures.settings);
    },
  };
});

import { setLocale } from '../i18n';
import { DEFAULT_LOCALE } from '../i18n/locale';

// English, so the expectations below read as the labels themselves rather than as a second copy
// of hu.json. This file is about which settings the page offers and what it loads, not about how
// each language words them — messages.test.ts is what holds the wording to account.
beforeEach(() => setLocale('en'));
afterEach(() => setLocale(DEFAULT_LOCALE));
import SettingsPage from './SettingsPage.vue';

async function settle() {
  await new Promise((resolve) => setTimeout(resolve, 0));
  await nextTick();
}

beforeEach(() => {
  calls.fetchRates = 0;
  calls.refreshRates = 0;
  calls.updateSettings = 0;
});

describe('SettingsPage', () => {
  it('shows the stored preference and the rates on record', async () => {
    const wrapper = mount(SettingsPage);
    await settle();

    const text = wrapper.text();

    expect(text).toContain('Base currency');
    expect(text).toContain(`1 ${f.exchangeRates[0].baseCurrency} = ${f.exchangeRates[0].rate}`);
  });

  it('reloads the rate list after the reporting currency is saved', async () => {
    const wrapper = mount(SettingsPage);
    await settle();

    expect(calls.fetchRates).toBe(1);

    await wrapper.find('button').trigger('click');
    await settle();

    expect(calls.updateSettings).toBe(1);
    expect(calls.fetchRates).toBe(2);
  });

  it('does not remount the rate list for its own actions', async () => {
    // Regression test: ExchangeRatesWidget used to be wired so that its own actions (not just
    // CurrencySettingsWidget's) bumped SettingsPage's remount key. Since the widget already
    // re-fetches itself after saving, that remount was pure waste — a second, redundant fetch —
    // and worse, it wiped the widget's own local state (the add-rate form, including the "use
    // the live rate" checkbox) back to defaults right after a successful action, which is what
    // read as "I checked it and it un-checked itself."
    const wrapper = mount(SettingsPage);
    await settle();

    expect(calls.fetchRates).toBe(1);

    await wrapper.find('input[type="number"]').setValue(400);
    await wrapper.find('form').trigger('submit');
    await settle();

    // One fetch from the widget's own load() after saving — not a second one from a remount
    // that was never supposed to happen for the rates widget's own actions.
    expect(calls.fetchRates).toBe(2);
  });
});
