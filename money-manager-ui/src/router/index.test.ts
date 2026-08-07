/**
 * The navigation guard is the only thing stopping an unauthenticated visitor from landing on a
 * page that will then fire authenticated requests and bounce them to /login via the 401
 * interceptor. Worth pinning: it is four lines and easy to get subtly wrong when a route is added.
 *
 * Note this is a client-side convenience, not a security boundary — the API's deny-by-default
 * FallbackPolicy is what actually protects the data. Changing this guard cannot leak anything;
 * it can only make the app feel broken.
 *
 * The page components are stubbed so this stays a routing test rather than mounting the app.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../components/Dashboard.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/BankAccounts.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/LoanPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/RentalPropertyPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/PropertyDetailPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/StockPage.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/UpcomingEvents.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/Login.vue', () => ({ default: { template: '<div />' } }));
vi.mock('../components/Register.vue', () => ({ default: { template: '<div />' } }));

const router = (await import('./index')).default;

beforeEach(async () => {
  localStorage.clear();
  router.push('/login');
  await router.isReady();
});

const go = async (path: string) => {
  await router.push(path).catch(() => {});
  return router.currentRoute.value;
};

describe('unauthenticated', () => {
  it.each(['/', '/accounts', '/loans', '/properties', '/properties/1', '/stocks', '/events'])(
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

  it.each(['/', '/accounts', '/loans', '/properties', '/stocks', '/events'])(
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
});
