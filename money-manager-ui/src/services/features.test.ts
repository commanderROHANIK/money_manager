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

  it('stays closed but tries again when the request fails', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    let attempt = 0;

    const { ensureFeaturesLoaded, featureFlags, requestCount } = await load(async () => {
      attempt += 1;
      if (attempt === 1) {
        throw new Error('network');
      }
      return mvp;
    });

    // A failure must not be cached. The flags gate the whole application, so a blip on the first
    // navigation would otherwise leave the session showing a one-page product until a reload —
    // and nothing on screen would explain why.
    expect(await ensureFeaturesLoaded()).toEqual(closed);
    expect(featureFlags.value).toEqual(closed);

    expect(await ensureFeaturesLoaded()).toEqual(mvp);
    expect(requestCount()).toBe(2);
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
