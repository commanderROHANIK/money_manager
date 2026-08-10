/**
 * The sidebar is what a customer reads the product's shape off, so the assertion that matters is
 * a negative one: a section this deployment switched off has no link, and nothing about the
 * remaining links depends on which sections those were.
 *
 * Driven through the real feature service rather than a stubbed one — the value here is that the
 * navigation and the flags are wired together, and a mock of the module under discussion would
 * assert only that the test file agrees with itself.
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import type { AxiosAdapter } from 'axios';
import type { Features } from '../services/features';

// Menu calls useRoute() to mark the active section. Providing a route is all it needs; building a
// real router here would pull in every page component for a highlight rule.
vi.mock('vue-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('vue-router')>();
  return { ...actual, useRoute: () => ({ path: '/' }) };
});

vi.mock('../services/authService', () => ({
  isLoggedIn: () => true,
  logout: vi.fn(),
}));

const mvp: Features = { banking: false, stocks: false, loans: true, events: true };
const everything: Features = { banking: true, stocks: true, loans: true, events: true };

/**
 * Loads a fresh module graph with the flags already resolved, the way the router guard leaves
 * them before the first authenticated view renders.
 */
async function mountMenuWith(flags: Features) {
  vi.resetModules();

  const { api } = await import('../services/api');

  const adapter: AxiosAdapter = async (config) => ({
    data: flags,
    status: 200,
    statusText: 'OK',
    headers: {},
    config,
  });

  api.defaults.adapter = adapter;

  const { ensureFeaturesLoaded } = await import('../services/features');
  await ensureFeaturesLoaded();

  const { i18n, setLocale } = await import('../i18n');

  // English, so the expectations below read as the labels themselves rather than as a second
  // copy of hu.json. Which language each link is written in is the locale files' business and is
  // covered by the parity test; this file is about which links exist at all.
  setLocale('en');

  const Menu = (await import('./Menu.vue')).default;

  return mount(Menu, {
    global: { plugins: [i18n], stubs: { RouterLink: { template: '<a><slot /></a>' } } },
  });
}

afterEach(() => {
  vi.restoreAllMocks();
});

const labels = (wrapper: Awaited<ReturnType<typeof mountMenuWith>>) =>
  wrapper.findAll('a').map((link) => link.text());

describe('Menu', () => {
  it('offers only the MVP sections when the rest are switched off', async () => {
    const wrapper = await mountMenuWith(mvp);

    expect(labels(wrapper)).toEqual(['Dashboard', 'Loans', 'Properties', 'Events', 'Settings']);
  });

  it('does not link to a section the deployment has switched off', async () => {
    const wrapper = await mountMenuWith(mvp);

    // The point of the whole change: a customer shown the rental MVP should find no trace of the
    // half-built sections, including a link that would take them to a page of 404s.
    expect(wrapper.html()).not.toContain('/accounts');
    expect(wrapper.html()).not.toContain('/stocks');
  });

  it('offers every section when they are all switched on', async () => {
    const wrapper = await mountMenuWith(everything);

    // Flags on has to be the application as it was before the gate existed. If this list ever
    // disagrees with the routes, one of the two has been edited alone.
    expect(labels(wrapper)).toEqual([
      'Dashboard',
      'Accounts',
      'Loans',
      'Properties',
      'Stocks',
      'Events',
      'Settings',
    ]);
  });

  it('keeps the sections that are not gated at all', async () => {
    const wrapper = await mountMenuWith({
      banking: false,
      stocks: false,
      loans: false,
      events: false,
    });

    // Properties and Settings carry no flag. A deployment with every switch off is still the
    // product it exists to sell, rather than an empty shell.
    expect(labels(wrapper)).toEqual(['Dashboard', 'Properties', 'Settings']);
  });
});
