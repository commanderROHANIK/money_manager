/**
 * The navigation guard is the only thing stopping an unauthenticated visitor from landing on a
 * page that will then fire authenticated requests and bounce them to /login via the 401
 * interceptor. Worth pinning: it is four lines and easy to get subtly wrong when a route is added.
 *
 * Note this is a client-side convenience, not a security boundary — the API's deny-by-default
 * FallbackPolicy is what actually protects the data. Changing this guard cannot leak anything;
 * it can only make the app feel broken.
 *
 * The guard also decides the sections this deployment does not present, which is the second half
 * of switching one off: hiding the navigation link leaves the URL itself working, so a bookmark
 * or a typed path would still mount a view whose every request the API now answers 404.
 *
 * The page components are stubbed so this stays a routing test rather than mounting the app.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { AxiosAdapter } from 'axios';
import { api } from '../services/api';
import { clearFeatures } from '../services/features';
import type { Features } from '../services/features';

vi.mock('../components/Dashboard.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/BankAccounts.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/LoanPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/RentalPropertyPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/PropertyDetailPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/StockPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/UpcomingEvents.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/SettingsPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/Login.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/Register.vue', () => ({ default: { template: '<div />' } }));

const everything: Features = {
  banking: true,
  stocks: true,
  loans: true,
  events: true,
  automaticExchangeRates: true,
};
const mvp: Features = {
  banking: false,
  stocks: false,
  loans: true,
  events: true,
  automaticExchangeRates: true,
};

/**
 * What GET /api/Features answers for the next navigation. Everything on by default, so the tests
 * that predate the flags assert what they always did — the whole application, unchanged.
 */
let flags: Features = everything;

const adapter: AxiosAdapter = async (config) => ({
  data: flags,
  status: 200,
  statusText: 'OK',
  headers: {},
  config,
});

api.defaults.adapter = adapter;

const router = (await import('./index')).default;

beforeEach(async () => {
  localStorage.clear();
  flags = everything;
  // The service caches the flags for the session, which is the point of it. Each test needs its
  // own answer, so the cache is dropped rather than worked around.
  clearFeatures();
  router.push('/login');
  await router.isReady();
});

const go = async (path: string) => {
  await router.push(path).catch(() => {});
  return router.currentRoute.value;
};

describe('unauthenticated', () => {
  it.each(['/', '/accounts', '/loans', '/properties', '/properties/1', '/stocks', '/events', '/settings'])(
    'redirects %s to the login screen',
    async (path) => {
      expect((await go(path)).path).toBe('/login');
    }
  );

  it('allows the login and register screens', async () => {
    expect((await go('/login')).path).toBe('/login');
    expect((await go('/register')).path).toBe('/register');
  });
});

describe('authenticated', () => {
  beforeEach(() => localStorage.setItem('token', 'a-token'));

  it.each(['/', '/accounts', '/loans', '/properties', '/stocks', '/events', '/settings'])(
    'allows %s',
    async (path) => {
      expect((await go(path)).path).toBe(path);
    }
  );

  it('sends a logged-in user away from the login and register screens', async () => {
    expect((await go('/login')).path).toBe('/');
    expect((await go('/register')).path).toBe('/');
  });

  it('exposes the property id as a route param', async () => {
    const route = await go('/properties/42');

    expect(route.name).toBe('PropertyDetail');
    expect(route.params.id).toBe('42');
  });
});

describe('sections this deployment does not present', () => {
  beforeEach(() => {
    localStorage.setItem('token', 'a-token');
    flags = mvp;
  });

  it.each(['/accounts', '/stocks'])('refuses %s and lands on the dashboard', async (path) => {
    // The dashboard is the safe destination: it is the one authenticated view that exists on
    // every deployment whatever the flags say.
    expect((await go(path)).path).toBe('/');
  });

  it.each(['/loans', '/events'])('still allows %s, which the MVP keeps', async (path) => {
    // The guard has to be a filter rather than a blanket, or switching one section off would
    // take the others with it.
    expect((await go(path)).path).toBe(path);
  });

  it.each(['/properties', '/settings'])('leaves the ungated %s alone', async (path) => {
    flags = {
      banking: false,
      stocks: false,
      loans: false,
      events: false,
      automaticExchangeRates: false,
    };

    expect((await go(path)).path).toBe(path);
  });

  it('checks the session before it asks for the flags', async () => {
    localStorage.clear();

    // Ordering matters: the flags are fetched with the caller's token, so consulting them first
    // would spend a 401 — and the interceptor's hard redirect — on every navigation made by
    // someone who is simply not logged in yet.
    expect((await go('/properties')).path).toBe('/login');
  });
});

describe('route table', () => {
  it('guards every route except login and register', () => {
    // A new page added without meta.requiresAuth renders for anyone until its first request
    // 401s, which reads as a broken page rather than a missing guard.
    const unguarded = router
      .getRoutes()
      .filter((r) => !r.meta.requiresAuth)
      .map((r) => r.path)
      .sort();

    expect(unguarded).toEqual(['/login', '/register']);
  });

  it('gives every gated route a flag the navigation also knows', () => {
    // The sidebar filters its links by the same names. A route gated under a name the menu does
    // not use — or the reverse — hides one of the two and not the other, which is how a link to
    // a dead page survives review.
    const gated = router
      .getRoutes()
      .filter((r) => r.meta.feature)
      .map((r) => [r.path, r.meta.feature])
      .sort();

    expect(gated).toEqual([
      ['/accounts', 'banking'],
      ['/events', 'events'],
      ['/loans', 'loans'],
      ['/stocks', 'stocks'],
    ]);
  });
});
