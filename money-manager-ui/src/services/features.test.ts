/**
 * The feature flags decide what the application contains, so the failure modes worth pinning are
 * about *when* the answer is wrong rather than whether the request is well-formed.
 *
 * Three of them: showing a section the deployment has switched off, asking the server once per
 * component instead of once per session, and getting stuck closed after a blip so the customer
 * sees a product with one page in it.
 *
 * Each test loads a fresh copy of the module, because the cache being tested is module state.
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import type { AxiosAdapter } from 'axios';
import type { Features } from './features';

const mvp: Features = { banking: false, stocks: false, loans: true, events: true };
const closed: Features = { banking: false, stocks: false, loans: false, events: false };

/**
 * A fresh module graph with a counting adapter swapped into axios, so "how many requests" is
 * observable and one test's cache cannot answer another test's question.
 */
async function load(respond: () => Promise<Features>) {
  vi.resetModules();

  const { api } = await import('./api');
  let requests = 0;

  const adapter: AxiosAdapter = async (config) => {
    requests += 1;
    const data = await respond();
    return { data, status: 200, statusText: 'OK', headers: {}, config };
  };

  api.defaults.adapter = adapter;

  const features = await import('./features');

  return { ...features, requestCount: () => requests };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('feature flags', () => {
  it('holds everything closed until the server answers', async () => {
    const { featureFlags } = await load(async () => mvp);

    // Nothing has been awaited, so this is the state the first paint would render from. Closed
    // rather than open on purpose: guessing "on" would flash a link for a section that is not
    // there, which is the one thing switching a section off has to prevent.
    expect(featureFlags.value).toEqual(closed);
  });

  it('reports what the deployment actually enabled', async () => {
    const { ensureFeaturesLoaded, featureFlags } = await load(async () => mvp);

    await ensureFeaturesLoaded();

    expect(featureFlags.value).toEqual(mvp);
  });

  it('asks the server once however many callers there are', async () => {
    const { ensureFeaturesLoaded, requestCount } = await load(async () => mvp);

    // The router guard, the navigation and the dashboard all want these on the same navigation.
    await Promise.all([ensureFeaturesLoaded(), ensureFeaturesLoaded(), ensureFeaturesLoaded()]);
    await ensureFeaturesLoaded();

    expect(requestCount()).toBe(1);
  });

  it('recovers from a single dropped request without the caller noticing', async () => {
    let attempt = 0;

    const { ensureFeaturesLoaded, featureFlags, requestCount } = await load(async () => {
      attempt += 1;
      if (attempt === 1) {
        throw new Error('network');
      }
      return mvp;
    });

    // The case a review caught: the router guard consumes *this* navigation's answer, so one
    // dropped request used to bounce someone off /loans to the dashboard with the sidebar
    // collapsed — and the retry only came on a navigation they had no reason to make. Retrying
    // inside the same call is what makes a blip invisible instead of disorienting.
    expect(await ensureFeaturesLoaded()).toEqual(mvp);
    expect(featureFlags.value).toEqual(mvp);
    expect(requestCount()).toBe(2);
  });

  it('holds the closed default when it fails twice, and starts fresh next time', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    let attempt = 0;

    const { ensureFeaturesLoaded, featureFlags, requestCount } = await load(async () => {
      attempt += 1;
      if (attempt <= 2) {
        throw new Error('network');
      }
      return mvp;
    });

    // Two failures in a row is an outage rather than a blip, and closed is the right thing to
    // hold: guessing "on" renders a section whose every endpoint answers 404.
    expect(await ensureFeaturesLoaded()).toEqual(closed);
    expect(featureFlags.value).toEqual(closed);
    expect(requestCount()).toBe(2);

    // But the failure is still not cached, so the next navigation is a clean slate.
    expect(await ensureFeaturesLoaded()).toEqual(mvp);
  });

  it('does not retry a 401, which the interceptor is already handling', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { ensureFeaturesLoaded, requestCount } = await load(async () => {
      throw Object.assign(new Error('unauthorised'), { response: { status: 401 } });
    });

    // A second request would only race the interceptor's redirect to the login screen with
    // another guaranteed failure.
    expect(await ensureFeaturesLoaded()).toEqual(closed);
    expect(requestCount()).toBe(1);
  });

  it('forgets the flags on logout', async () => {
    const { ensureFeaturesLoaded, clearFeatures, featureFlags, requestCount } =
      await load(async () => mvp);

    await ensureFeaturesLoaded();
    expect(featureFlags.value).toEqual(mvp);

    clearFeatures();

    // Back to closed, and the next caller asks again rather than rendering the previous
    // session's answer.
    expect(featureFlags.value).toEqual(closed);

    await ensureFeaturesLoaded();
    expect(requestCount()).toBe(2);
  });
});
